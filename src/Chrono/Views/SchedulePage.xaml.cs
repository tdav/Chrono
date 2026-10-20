using Chrono.ViewModels;

namespace Chrono.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel viewModel;
    private IDispatcherTimer? timer;

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

        // «Текущее» дело и «осталось N мин» меняются со временем, пока экран открыт.
        this.timer ??= this.Dispatcher.CreateTimer();
        this.timer.Interval = TimeSpan.FromSeconds(30);
        this.timer.Tick -= this.OnTick;
        this.timer.Tick += this.OnTick;
        this.timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.timer?.Stop();
    }

    private void OnTick(object? sender, EventArgs e) => this.viewModel.Refresh();
}
