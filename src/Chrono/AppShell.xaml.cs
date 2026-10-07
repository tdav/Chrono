using Chrono.Views;

namespace Chrono;

public partial class AppShell : Shell
{
    public AppShell()
    {
        this.InitializeComponent();
        Routing.RegisterRoute("edit", typeof(AlarmEditPage));
        Routing.RegisterRoute("ring", typeof(RingPage));
        Routing.RegisterRoute("reliability", typeof(ReliabilityPage));
    }
}
