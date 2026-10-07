using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// Окно умного пробуждения: за 30 минут до будильника раз в минуту оценивает фазу сна
/// (стадия и пульс с часов из Health Connect + движение телефона) и при лёгком сне будит раньше.
/// Если лёгкий сон не найден, сервис закрывается к сроку, и звонит обычный будильник на заданное время.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSystemExempted)]
public sealed class SmartWakeService : Service, ISensorEventListener
{
    public const string ChannelId = "smart_wake";
    public const int NotificationId = 3;

    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HealthReadInterval = TimeSpan.FromMinutes(3);

    private readonly List<int> movementPerMinute = [];
    private readonly List<HeartRateSample> heartRate = [];
    private Guid alarmId;
    private DateTimeOffset deadline;
    private double? baselineBpm;
    private WatchSleepStage stage;
    private DateTimeOffset lastHealthRead;
    private bool healthAvailable;
    private int movementThisMinute;
    private float[]? previousAcceleration;
    private SensorManager? sensors;
    private Handler? handler;
    private PowerManager.WakeLock? wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notifications = (NotificationManager)this.GetSystemService(NotificationService)!;
        notifications.CreateNotificationChannel(new NotificationChannel(ChannelId, AppResources.ChannelSmartWake, NotificationImportance.Low));

        var alarm = Guid.TryParse(intent?.GetStringExtra(AlarmReceiver.ExtraAlarmId), out var id)
            ? new AlarmStore(FileSystem.AppDataDirectory).Load().Alarms.FirstOrDefault(a => a.Id == id)
            : null;

        var notification = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_alarm)!
            .SetContentTitle(AppResources.SmartWake)!
            .SetContentText(string.Format(AppResources.SmartWakeNotification, (alarm?.At ?? DateTime.Now).ToString("HH:mm")))!
            .SetOngoing(true)!
            .Build()!;

        // StartForeground — до любых ранних выходов, иначе система роняет процесс.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            this.StartForeground(NotificationId, notification, ForegroundService.TypeSystemExempted);
        }
        else
        {
            this.StartForeground(NotificationId, notification);
        }

        if (alarm is not { IsEnabled: true } || !SmartWakeSettings.Enabled || this.handler is not null)
        {
            // Будильник выключен/удалён, функция отключена или окно уже идёт — второй сервис не нужен.
            if (this.handler is null)
            {
                this.StopSelf();
            }

            return StartCommandResult.NotSticky;
        }

        this.alarmId = alarm.Id;
        this.deadline = new DateTimeOffset(DateTime.SpecifyKind(alarm.At, DateTimeKind.Local));

        var power = (PowerManager)this.GetSystemService(PowerService)!;
        this.wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "chrono:smartwake")!;
        this.wakeLock.Acquire((long)(this.deadline - DateTimeOffset.Now + TimeSpan.FromMinutes(1)).TotalMilliseconds);

        this.sensors = (SensorManager)this.GetSystemService(SensorService)!;
        var accelerometer = this.sensors.GetDefaultSensor(SensorType.Accelerometer);
        if (accelerometer is not null)
        {
            this.sensors.RegisterListener(this, accelerometer, SensorDelay.Normal);
        }

        this.healthAvailable = HealthConnect.IsAvailable(this);
        this.handler = new Handler(Looper.MainLooper!);
        this.handler.PostDelayed(this.OnTick, (long)Tick.TotalMilliseconds);

        if (this.healthAvailable)
        {
            // Медиана пульса за 4 часа до окна — ночная база, с которой сравнивается свежий пульс.
            _ = this.ReadHealthAsync(baseline: true);
        }

        return StartCommandResult.NotSticky;
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        var values = e?.Values;
        if (values is null || values.Count < 3)
        {
            return;
        }

        var current = new[] { values[0], values[1], values[2] };
        if (this.previousAcceleration is { } previous
            && SleepPhaseEstimator.IsMovement(previous[0], previous[1], previous[2], current[0], current[1], current[2]))
        {
            this.movementThisMinute++;
        }

        this.previousAcceleration = current;
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
    {
    }

    public override void OnDestroy()
    {
        this.sensors?.UnregisterListener(this);
        this.handler?.RemoveCallbacksAndMessages(null);
        if (this.wakeLock?.IsHeld == true)
        {
            this.wakeLock.Release();
        }

        this.StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    // Обработчик таймера: передаётся в Handler.PostDelayed, поэтому отдельный метод.
    private void OnTick()
    {
        this.movementPerMinute.Add(this.movementThisMinute);
        this.movementThisMinute = 0;

        var now = DateTimeOffset.Now;
        var alarm = new AlarmStore(FileSystem.AppDataDirectory).Load().Alarms.FirstOrDefault(a => a.Id == this.alarmId);
        if (now >= this.deadline - TimeSpan.FromSeconds(30) || alarm is not { IsEnabled: true } || !SmartWakeSettings.Enabled)
        {
            // Срок наступил (звонит обычный будильник) или будильник/функцию выключили.
            this.StopSelf();
            return;
        }

        if (this.healthAvailable && now - this.lastHealthRead >= HealthReadInterval)
        {
            _ = this.ReadHealthAsync(baseline: false);
        }

        var phase = SleepPhaseEstimator.Estimate(now, this.stage, this.movementPerMinute, this.heartRate, this.baselineBpm);
        global::Android.Util.Log.Info("Chrono", $"Умное пробуждение: фаза {phase}, движение {this.movementPerMinute[^1]}, пульс {this.heartRate.Count} отсч., база {this.baselineBpm}, стадия {this.stage}");
        if (phase == SleepPhase.Light)
        {
            // Сигнал на срок снимается: разовый будильник выключается, повторяющийся переходит
            // на следующее срабатывание. Затем звонит сразу — тем же путём, что и обычный:
            // точный будильник → AlarmReceiver → AlarmRingService.
            var service = new Services.AlarmService(new AlarmStore(FileSystem.AppDataDirectory), new AlarmScheduler(), TimeProvider.System);
            if (alarm.Repeat == RepeatKind.None)
            {
                service.Toggle(this.alarmId, false);
            }
            else
            {
                service.Save(alarm with { At = alarm.NextAfter(alarm.At) });
            }
            var manager = (AlarmManager)this.GetSystemService(Context.AlarmService)!;
            var showIntent = PendingIntent.GetActivity(this, 3, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable);
            manager.SetAlarmClock(
                new AlarmManager.AlarmClockInfo(DateTimeOffset.Now.AddSeconds(1).ToUnixTimeMilliseconds(), showIntent),
                AlarmReceiver.CreatePendingIntent(this, this.alarmId));
            this.StopSelf();
            return;
        }

        this.handler!.PostDelayed(this.OnTick, (long)Tick.TotalMilliseconds);
    }

    private async Task ReadHealthAsync(bool baseline)
    {
        var now = DateTimeOffset.Now;
        this.lastHealthRead = now;
        try
        {
            if (baseline)
            {
                this.baselineBpm = SleepPhaseEstimator.Baseline(await HealthConnect.ReadHeartRateAsync(this, now.AddHours(-4), now));
                return;
            }

            var fresh = await HealthConnect.ReadHeartRateAsync(this, now - SleepPhaseEstimator.FreshHeartRate, now);
            this.heartRate.Clear();
            this.heartRate.AddRange(fresh);
            this.stage = await HealthConnect.ReadCurrentStageAsync(this, now);
        }
        catch (Exception ex)
        {
            // Нет разрешения, лимит запросов или сбой Health Connect — окно продолжает работать по движению.
            global::Android.Util.Log.Warn("Chrono", $"Health Connect недоступен: {ex.Message}");
        }
    }
}
