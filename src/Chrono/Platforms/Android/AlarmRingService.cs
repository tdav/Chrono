using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Chrono.Resources.Strings;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// Foreground-сервис звонка: мелодия в цикле, вибрация, уведомление с full-screen intent.
/// Живёт только пока звенит сигнал, не дольше 10 минут.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSystemExempted)]
public sealed class AlarmRingService : Service
{
    public const string ActionStop = "chrono.action.STOP";
    public const string AlarmChannelId = "alarm";
    public const string MissedChannelId = "missed";
    public const int RingNotificationId = 1;
    public const int MissedNotificationId = 2;
    public static readonly TimeSpan RingTimeout = TimeSpan.FromMinutes(10);

    private readonly SoundPlayer soundPlayer = new();
    private Vibrator? vibrator;
    private PowerManager.WakeLock? wakeLock;
    private Handler? timeoutHandler;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            this.StopSelf();
            return StartCommandResult.NotSticky;
        }

        var notifications = (NotificationManager)this.GetSystemService(NotificationService)!;
        var alarmChannel = new NotificationChannel(AlarmChannelId, AppResources.ChannelAlarms, NotificationImportance.High);
        alarmChannel.SetSound(null, null);
        notifications.CreateNotificationChannel(alarmChannel);
        notifications.CreateNotificationChannel(
            new NotificationChannel(MissedChannelId, AppResources.ChannelMissed, NotificationImportance.Default));

        var alarmIdText = intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId);
        var alarm = Guid.TryParse(alarmIdText, out var alarmId)
            ? new AlarmStore(FileSystem.AppDataDirectory).Load().Alarms.FirstOrDefault(a => a.Id == alarmId)
            : null;
        var label = string.IsNullOrWhiteSpace(alarm?.Label) ? AppResources.DefaultLabel : alarm.Label;
        var timeText = (alarm?.At ?? DateTime.Now).ToString("HH:mm");

        var fullScreenIntent = PendingIntent.GetActivity(
            this,
            0,
            new Intent(this, typeof(MainActivity))
                .PutExtra(AlarmReceiver.ExtraAlarmId, alarmIdText)!
                .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var stopIntent = PendingIntent.GetService(
            this, 1, new Intent(this, typeof(AlarmRingService)).SetAction(ActionStop), PendingIntentFlags.Immutable);

        var builder = new Notification.Builder(this, AlarmChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_alarm)!
            .SetContentTitle(timeText)!
            .SetContentText(label)!
            .SetCategory(Notification.CategoryAlarm)!
            .SetOngoing(true)!
            .SetFullScreenIntent(fullScreenIntent, true)!
            .SetContentIntent(fullScreenIntent)!;

        // С примером для отключения кнопки «Стоп» в уведомлении нет: она выключала бы сигнал без решения.
        if (!Services.DismissSettings.MathChallenge)
        {
            builder.AddAction(new Notification.Action.Builder(
                global::Android.Graphics.Drawables.Icon.CreateWithResource(this, Resource.Drawable.ic_stat_alarm),
                AppResources.Stop,
                stopIntent).Build());
        }

        var notification = builder.Build()!;

        // StartForeground обязан прозвучать в течение нескольких секунд после StartForegroundService —
        // вызываем его до любых ранних выходов, иначе система роняет процесс.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            this.StartForeground(RingNotificationId, notification, ForegroundService.TypeSystemExempted);
        }
        else
        {
            this.StartForeground(RingNotificationId, notification);
        }

        // Повторный старт (второй будильник в ту же минуту) заменяет текущий сигнал.
        this.soundPlayer.Stop();
        this.vibrator?.Cancel();
        this.timeoutHandler?.RemoveCallbacksAndMessages(null);

        if (alarm is null)
        {
            this.StopSelf();
            return StartCommandResult.NotSticky;
        }

        // Повторяющийся переходит на следующее срабатывание и планируется сразу, не дожидаясь открытия приложения.
        new AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System).RescheduleAll();

        // Повторный старт (второй будильник) продлевает удержание CPU на полный срок нового сигнала.
        if (this.wakeLock?.IsHeld == true)
        {
            this.wakeLock.Release();
        }

        var power = (PowerManager)this.GetSystemService(PowerService)!;
        this.wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "chrono:ring")!;
        this.wakeLock.Acquire((long)(RingTimeout + TimeSpan.FromSeconds(10)).TotalMilliseconds);

        if (alarm.SoundEnabled)
        {
            try
            {
                this.soundPlayer.Play(alarm.SoundId, loop: true);
            }
            catch (Exception ex)
            {
                // Звук не запустился даже запасным путём — вибрация и уведомление продолжают работу.
                global::Android.Util.Log.Error("Chrono", $"Мелодия не запустилась: {ex}");
            }
        }

        if (alarm.VibrationEnabled)
        {
            this.vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? ((VibratorManager)this.GetSystemService(VibratorManagerService)!).DefaultVibrator
                : (Vibrator)this.GetSystemService(VibratorService)!;
            // Без атрибутов будильника система считает вибрацию UNKNOWN и может подавить её в «Не беспокоить».
            var pattern = VibrationEffect.CreateWaveform([0, 800, 600], 0)!;
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                this.vibrator.Vibrate(pattern, VibrationAttributes.CreateForUsage((int)VibrationAttributesUsageType.Alarm));
            }
            else
            {
                this.vibrator.Vibrate(pattern, new AudioAttributes.Builder().SetUsage(AudioUsageKind.Alarm)!.Build()!);
            }
        }

        this.timeoutHandler = new Handler(Looper.MainLooper!);
        this.timeoutHandler.PostDelayed(
            () =>
            {
                var missed = new Notification.Builder(this, MissedChannelId)
                    .SetSmallIcon(Resource.Drawable.ic_stat_alarm)!
                    .SetContentTitle(string.Format(AppResources.MissedAlarm, timeText))!
                    .SetContentText(label)!
                    .SetContentIntent(PendingIntent.GetActivity(
                        this, 2, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable))!
                    .SetAutoCancel(true)!
                    .Build()!;
                notifications.Notify(MissedNotificationId, missed);
                this.StopSelf();
            },
            (long)RingTimeout.TotalMilliseconds);

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        this.soundPlayer.Stop();
        this.vibrator?.Cancel();
        this.timeoutHandler?.RemoveCallbacksAndMessages(null);
        if (this.wakeLock?.IsHeld == true)
        {
            this.wakeLock.Release();
        }

        this.StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }
}
