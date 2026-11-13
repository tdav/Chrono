using Chrono.Services;
using Chrono.Views;

namespace Chrono;

public partial class AppShell : Shell
{
    // Маршруты регистрируются один раз на процесс: AppShell создаётся заново для каждого окна
    // (после «Назад» процесс жив, а новое окно не может взять Shell, привязанный к старому).
    static AppShell()
    {
        Routing.RegisterRoute("edit", typeof(AlarmEditPage));
        Routing.RegisterRoute("ring", typeof(RingPage));
        Routing.RegisterRoute("reliability", typeof(ReliabilityPage));
    }

    public AppShell()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Отложенные переходы: сигнал, настройки, день «Распорядка». Сигнал первым — остальные ждут, пока он на экране.
    /// Вызывается при каждом переходе Shell и при возобновлении активити (MainActivity.OnPostResume).
    /// </summary>
    public static async Task ShowPendingAsync()
    {
        await RingLauncher.ShowPendingAsync();
        await SettingsLauncher.ShowPendingAsync();
        await ScheduleLauncher.ShowPendingAsync();
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        // Отдельным сообщением главного цикла: новый переход не начинается внутри завершения текущего.
        // Закрытие RingPage выполнит отложенный день или настройки; переход, не открывший сигнал, повторит его.
        this.Dispatcher.Dispatch(async () => await ShowPendingAsync());
    }
}
