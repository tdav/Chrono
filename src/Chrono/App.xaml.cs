using Chrono.Services;
using Chrono.Views;

namespace Chrono;

public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services, AlarmService alarmService)
    {
        this.InitializeComponent();
        this.services = services;
        // Приложение всегда тёмное: светлые иконки статус-бара и тёмные диалоги пикеров при любой системной теме.
        this.UserAppTheme = AppTheme.Dark;
        // Восстанавливаем расписание: после «Остановить принудительно» Android стирает будильники приложения.
        alarmService.RescheduleAll();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Запуск по сигналу — без splash, сразу Shell; RingPage откроет AlarmListPage через RingLauncher.
        Page first = RingLauncher.PendingId is null
            ? new SplashPage(this.services)
            : this.services.GetRequiredService<AppShell>();
        return new Window(first);
    }
}
