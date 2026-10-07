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
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }

    public AppShell()
    {
        this.InitializeComponent();
    }
}
