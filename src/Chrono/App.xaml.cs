using Chrono.Design;
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
    private readonly ThemeService themes;
    private bool syncAllowed;

    public App(IServiceProvider services, AlarmService alarmService, ThemeService themes)
    {
        StartTrace.Mark("App: конструктор, начало");
        this.InitializeComponent();
        this.services = services;
        this.themes = themes;
        // Тема — из настроек («Система» = как в телефоне), повышенный контраст — из системы (DESIGN.md §2.2, §5).
        // Экраны, ещё берущие цвета через StaticResource, перекрашиваются только при следующем открытии.
        this.UserAppTheme = ThemeSettings.Theme;
        themes.Attach(this);
        // Восстанавливаем расписание: после «Остановить принудительно» Android стирает будильники приложения.
        alarmService.RescheduleAll();
        StartTrace.Mark("App: RescheduleAll выполнен");

        // Правки пользователя уходят в Google через 3 с; правки из самой синхронизации (fromSync) Changed не вызывают.
        alarmService.Changed += (_, _) => this.services.GetRequiredService<SyncLauncher>().RequestSoon();
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
            // Контраст до Android 14 и «Отключить анимацию» не сообщают об изменении — перечитываем при возврате.
            this.themes.Refresh();
            if (this.syncAllowed)
            {
                this.services.GetRequiredService<SyncLauncher>().RequestSoon();
            }
        };
        StartTrace.Mark("App: CreateWindow, конец");
        return window;
    }
}
