using Chrono.ViewModels;

namespace Chrono.Views;

public partial class DialPage : ContentPage
{
    private readonly DialViewModel viewModel;
    private readonly DialDrawable drawable = new();
    private IDispatcherTimer? timer;
    private Window? window;

    public DialPage(DialViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
        this.Dial.Drawable = this.drawable;
        this.viewModel.DialChanged += this.OnDialChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.viewModel.Refresh();

        // Возврат из фона OnAppearing не вызывает. Page.Window у страницы Shell в OnAppearing ещё null — берём окно приложения (как в SchedulePage).
        this.window = Application.Current?.Windows.FirstOrDefault();
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
            this.window.Resumed += this.OnWindowResumed;
        }

        // Стрелка и «текущее» дело двигаются со временем: раз в минуту.
        this.timer ??= this.Dispatcher.CreateTimer();
        this.timer.Interval = TimeSpan.FromMinutes(1);
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

    private void OnDialChanged(object? sender, EventArgs e)
    {
        this.drawable.Slices = this.viewModel.Slices;
        this.drawable.SelectedId = this.viewModel.SelectedId;
        this.drawable.ArrowAngle = this.viewModel.ArrowAngle;
        this.Dial.Invalidate();
    }

    private void OnDialTapped(object? sender, TappedEventArgs e)
    {
        if (e.GetPosition(this.Dial) is not { } touch)
        {
            return;
        }

        var (angle, fraction) = DialDrawable.ToPolar(touch, this.Dial.Width, this.Dial.Height);
        this.viewModel.SelectAt(angle, fraction);
    }

    private void OnTick(object? sender, EventArgs e) => this.viewModel.Refresh();

    private void OnWindowResumed(object? sender, EventArgs e) => this.viewModel.Refresh();
}
