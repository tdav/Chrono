using Chrono.ViewModels;

namespace Chrono.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel viewModel;
    private IDispatcherTimer? timer;
    private Window? window;

    public SchedulePage(ScheduleViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.viewModel.Refresh();

        // Возврат из фона OnAppearing не вызывает: запись могла начаться или исчезнуть, пока приложение было свёрнуто.
        // Page.Window у страницы Shell в OnAppearing ещё null — берём окно приложения (как в SettingsPage).
        this.window = Application.Current?.Windows.FirstOrDefault();
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
            this.window.Resumed += this.OnWindowResumed;
        }

        // «Текущее» дело и «осталось N мин» меняются со временем, пока экран открыт.
        this.timer ??= this.Dispatcher.CreateTimer();
        this.timer.Interval = TimeSpan.FromSeconds(30);
        this.timer.Tick -= this.OnTick;
        this.timer.Tick += this.OnTick;
        this.timer.Start();
    }

    protected override void OnDisappearing()
    {
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
        }

        base.OnDisappearing();
        this.timer?.Stop();
    }

    private void OnTick(object? sender, EventArgs e) => this.viewModel.Refresh();

    private void OnWindowResumed(object? sender, EventArgs e) => this.viewModel.Refresh();
}
