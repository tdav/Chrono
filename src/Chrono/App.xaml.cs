using Chrono.Services;
using Chrono.Views;

namespace Chrono;

public partial class App : Application
{
    // Первая синхронизация и возвраты в приложение начинаются не раньше чем через это время после создания окна:
    // SyncLauncher тянет за собой CalendarSync, GoogleCalendarClient и HttpClient, а RequestSoon читает sync.json,
    // и всё это раньше выполнялось на главном потоке до первого кадра (≈0,3 с на эмуляторе).
    private static readonly TimeSpan StartupSyncDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceProvider services;
    private bool syncAllowed;

    public App(IServiceProvider services, AlarmService alarmService)
    {
        StartTrace.Mark("App: конструктор, начало");
        this.InitializeComponent();
        this.services = services;
        // Приложение всегда тёмное: светлые иконки статус-бара и тёмные диалоги пикеров при любой системной теме.
        this.UserAppTheme = AppTheme.Dark;
        // Восстанавливаем расписание: после «Остановить принудительно» Android стирает будильники приложения.
        alarmService.RescheduleAll();
        StartTrace.Mark("App: RescheduleAll выполнен");

        // Правки пользователя уходят в Google через 3 с; правки из самой синхронизации (fromSync) Changed не вызывают.
        alarmService.Changed += (_, _) => services.GetRequiredService<SyncLauncher>().RequestSoon();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Запуск по сигналу — без splash, сразу Shell; RingPage откроет AlarmListPage через RingLauncher.
        StartTrace.Mark("App: CreateWindow, начало");
        Page first = RingLauncher.PendingId is null
            ? new SplashPage(this.services)
            : this.services.GetRequiredService<AppShell>();
        var window = new Window(first);
        // Синхронизация при старте интерфейса, а не процесса: процесс, поднятый воркером, BootReceiver или сигналом
        // будильника, второй проход не запускает. Окно, созданное ради звонка (PendingId), её не заводит: звонок
        // не должен ждать сети, синхронизация начнётся при следующем Resumed. Без входа RequestSoon ничего не делает.
        // Запуск отложен на StartupSyncDelay, чтобы не занимать главный поток до первого кадра.
        var startupSync = RingLauncher.PendingId is null;
        this.Dispatcher.DispatchDelayed(StartupSyncDelay, () =>
        {
            this.syncAllowed = true;
            if (startupSync)
            {
                this.services.GetRequiredService<SyncLauncher>().RequestSoon();
            }
        });

        // Возврат в приложение: подтянуть изменения, сделанные в Google Календаре. Resumed первого запуска
        // (до StartupSyncDelay) пропускается: первая синхронизация и так стартует по таймеру выше.
        window.Resumed += (_, _) =>
        {
            if (this.syncAllowed)
            {
                this.services.GetRequiredService<SyncLauncher>().RequestSoon();
            }
        };
        StartTrace.Mark("App: CreateWindow, конец");
        return window;
    }
}
