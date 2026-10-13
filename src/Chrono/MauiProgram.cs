using Chrono.Platform;
using Chrono.Services;
using Chrono.ViewModels;
using Chrono.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace Chrono;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
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
        // Нативное подчёркивание Android у полей лишнее: поля уже стоят в карточках с рамкой.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));

        // Выключенный тумблер на белой карточке почти не виден (серая дорожка платформы): красим дорожку и бегунок
        // по состоянию. Вешаем на ключи MAUI (OnColor, ThumbColor, IsOn), чтобы отработать после их собственных
        // маппингов, которые сбрасывают тинт.
        foreach (var key in new[] { "OnColor", "ThumbColor", "IsOn" })
        {
            Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping(key, (handler, view) =>
            {
                var states = new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() };
                handler.PlatformView.TrackTintList = new Android.Content.Res.ColorStateList(states,
                    new[] { Android.Graphics.Color.ParseColor("#2E3136").ToArgb(), Android.Graphics.Color.ParseColor("#D9D3C8").ToArgb() });
                handler.PlatformView.ThumbTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.White);
            });
        }
#endif

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_ => new AlarmStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<ISoundPlayer, SoundPlayer>();
        builder.Services.AddSingleton<IAlarmScheduler, AlarmScheduler>();
        builder.Services.AddSingleton<IReliabilityChecks, ReliabilityChecks>();
        builder.Services.AddSingleton<ISmartWake, SmartWake>();
        builder.Services.AddSingleton<AlarmService>();

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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
