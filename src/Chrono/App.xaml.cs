using Chrono.Services;
using Chrono.Views;

namespace Chrono;

public partial class App : Application
{
    private readonly IServiceProvider services;
    private readonly SyncLauncher syncLauncher;

    public App(IServiceProvider services, AlarmService alarmService, SyncLauncher syncLauncher)
    {
        this.InitializeComponent();
        this.services = services;
        this.syncLauncher = syncLauncher;
        // Приложение всегда тёмное: светлые иконки статус-бара и тёмные диалоги пикеров при любой системной теме.
        this.UserAppTheme = AppTheme.Dark;
        // Восстанавливаем расписание: после «Остановить принудительно» Android стирает будильники приложения.
        alarmService.RescheduleAll();

        // Правки пользователя уходят в Google через 3 с; правки из самой синхронизации (fromSync) Changed не вызывают.
        alarmService.Changed += (_, _) => syncLauncher.RequestSoon();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Запуск по сигналу — без splash, сразу Shell; RingPage откроет AlarmListPage через RingLauncher.
        Page first = RingLauncher.PendingId is null
            ? new SplashPage(this.services)
            : this.services.GetRequiredService<AppShell>();
        var window = new Window(first);
        // Синхронизация при старте интерфейса, а не процесса: процесс, поднятый воркером, BootReceiver или сигналом
        // будильника, второй проход не запускает. Без входа RequestSoon ничего не делает.
        this.syncLauncher.RequestSoon();
        // Возврат в приложение: подтянуть изменения, сделанные в Google Календаре.
        window.Resumed += (_, _) => this.syncLauncher.RequestSoon();
        return window;
    }
}
