namespace Chrono;

// Промежуточная версия каркаса (задача 1). Финальная версия — в задаче 7.
public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
