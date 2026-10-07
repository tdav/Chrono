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
                fonts.AddFont("Manrope-Light.ttf", "ManropeLight");
                fonts.AddFont("Manrope-Regular.ttf", "ManropeRegular");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemiBold");
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
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_ => new AlarmStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<ISoundPlayer, SoundPlayer>();
        builder.Services.AddSingleton<IAlarmScheduler, AlarmScheduler>();
        builder.Services.AddSingleton<IReliabilityChecks, ReliabilityChecks>();
        builder.Services.AddSingleton<AlarmService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<AlarmListViewModel>();
        builder.Services.AddTransient<AlarmListPage>();
        builder.Services.AddTransient<AlarmEditViewModel>();
        builder.Services.AddTransient<AlarmEditPage>();
        builder.Services.AddTransient<RingViewModel>();
        builder.Services.AddTransient<RingPage>();
        builder.Services.AddTransient<ReliabilityViewModel>();
        builder.Services.AddTransient<ReliabilityPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
