using Chrono.Platform;
using Chrono.Services;
using Chrono.ViewModels;
using Chrono.Views;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace Chrono;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        StartTrace.Mark("CreateMauiApp: начало");
        // Первое обращение к TimeZoneInfo.Local на Android разбирает tzdata (110-250 мс) и приходится на RescheduleAll
        // в конструкторе App; на фоновом потоке оно идёт параллельно с созданием приложения.
        _ = Task.Run(() => TimeZoneInfo.Local);
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Regular.ttf", "ManropeRegular");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemiBold");
                fonts.AddFont("Manrope-Medium.ttf", "ManropeMedium");
                fonts.AddFont("Manrope-Bold.ttf", "ManropeBold");
                fonts.AddFont("Manrope-ExtraBold.ttf", "ManropeExtraBold");
            })
            .ConfigureLifecycleEvents(events =>
            {
#if IOS
                events.AddiOS(ios => ios.FinishedLaunching((app, options) =>
                {
                    // Делегат назначается до окончания запуска — иначе нажатие на уведомление при холодном старте теряется.
                    UserNotifications.UNUserNotificationCenter.Current.Delegate = new NotificationDelegate();
                    return true;
                }));
#endif
            });

#if ANDROID
        // Нативное подчёркивание Android у полей лишнее: поля уже стоят в карточках с рамкой. Фон снимается целиком, а не
        // только красится прозрачным: у фона-подчёркивания нижний отступ больше верхнего, и текст поля стоял выше подписи строки.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetPadding(0, 0, 0, 0);
            handler.PlatformView.SetIncludeFontPadding(false);
        });

        // Подписи и значения строк форм (RowKey, RowValue): без верхнего запаса шрифта Manrope текст стоит по центру строки
        // вровень с шевроном, иконкой, полем и тумблером, а не ниже них.
        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping("RowFontPadding", (handler, view) =>
        {
            if (view is Label { Style: { } style } && Application.Current?.Resources is { } resources
                && ((resources.TryGetValue("RowKey", out var rowKey) && rowKey == style)
                    || (resources.TryGetValue("RowValue", out var rowValue) && rowValue == style)))
            {
                handler.PlatformView.SetIncludeFontPadding(false);
            }
        });

        // Выключенный тумблер на белой карточке почти не виден (серая дорожка платформы): красим дорожку и бегунок
        // по состоянию. Вешаем на ключи MAUI (OnColor, ThumbColor, IsOn), чтобы отработать после их собственных
        // маппингов, которые сбрасывают тинт.
        var trackTint = new Android.Content.Res.ColorStateList(
            new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() },
            new[] { Android.Graphics.Color.ParseColor("#2E3136").ToArgb(), Android.Graphics.Color.ParseColor("#D9D3C8").ToArgb() });
        var thumbTint = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.White);
        foreach (var key in new[] { "OnColor", "ThumbColor", "IsOn" })
        {
            Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping(key, (handler, view) =>
            {
                handler.PlatformView.TrackTintList = trackTint;
                handler.PlatformView.ThumbTintList = thumbTint;
            });
        }
#endif

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_ => new AlarmStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<ISoundPlayer, SoundPlayer>();
        builder.Services.AddSingleton<IAlarmScheduler, AlarmScheduler>();
        builder.Services.AddSingleton<IReliabilityChecks, ReliabilityChecks>();
        builder.Services.AddSingleton<ISmartWake, SmartWake>();
        builder.Services.AddSingleton(_ => new SyncStore(FileSystem.AppDataDirectory));
        builder.Services.AddMemoryCache();
        // Явная фабрика: SyncStore в AlarmService необязательный, и без неё очередь изменений для Google не наполнялась бы.
        builder.Services.AddSingleton(sp => new AlarmService(
            sp.GetRequiredService<AlarmStore>(),
            sp.GetRequiredService<IAlarmScheduler>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<SyncStore>()));
#if ANDROID
        builder.Services.AddSingleton<IGoogleAuth>(_ => new GoogleAuth());
#elif IOS
        builder.Services.AddSingleton<IGoogleAuth>(_ => new GoogleAuth(new HttpClient()));
#endif
        // Свой HttpClient: GoogleCalendarClient задаёт ему BaseAddress и заголовки. AddHttpClient не используем (нет пакета).
        builder.Services.AddSingleton(sp => new GoogleCalendarClient(
            new HttpClient(), sp.GetRequiredService<IMemoryCache>(), sp.GetRequiredService<IGoogleAuth>()));
        builder.Services.AddSingleton<CalendarSync>();
        builder.Services.AddSingleton(sp =>
        {
            var sync = sp.GetRequiredService<CalendarSync>();
            // CalendarSync рассчитывает на продолжения на главном потоке, а запуск идёт с таймера: переносим на главный.
            return new SyncLauncher(
                ct => MainThread.InvokeOnMainThreadAsync(() => sync.SyncAsync(ct)),
                sp.GetRequiredService<SyncStore>(),
                sp.GetRequiredService<TimeProvider>());
        });

        // Transient: новый Shell для каждого окна (см. AppShell).
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<AlarmListViewModel>();
        builder.Services.AddTransient<AlarmListPage>();
        builder.Services.AddTransient<AlarmEditViewModel>();
        builder.Services.AddTransient<AlarmEditPage>();
        builder.Services.AddTransient<RingViewModel>();
        builder.Services.AddTransient<RingPage>();
        builder.Services.AddTransient<ReliabilityViewModel>();
        builder.Services.AddTransient<ReliabilityPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ScheduleViewModel>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<DialViewModel>();
        builder.Services.AddTransient<DialPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        StartTrace.Mark("CreateMauiApp: конец");
        return app;
    }
}
