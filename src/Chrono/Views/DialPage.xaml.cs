using Chrono.Design;
using Chrono.Resources.Strings;
using Chrono.ViewModels;

namespace Chrono.Views;

public partial class DialPage : ContentPage
{
    private readonly DialViewModel viewModel;
    private readonly ThemeService themes;
    private readonly DialDrawable drawable = new();
    private IDispatcherTimer? timer;
    private Window? window;

    public DialPage(DialViewModel viewModel, ThemeService themes)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.themes = themes;
        this.BindingContext = viewModel;
        // Шапка сворачивается по прокрутке страницы. В XAML ссылка не подходит: шапка объявлена раньше прокрутки (на неё ссылается отступ).
        this.Header.ScrollView = this.Scroll;
        this.Halves.Items = [AppResources.DialNow, AppResources.DialEvening];
        this.Dial.Drawable = this.drawable;
        this.viewModel.DialChanged += this.OnDialChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.viewModel.Refresh();

        // Циферблат рисуется кодом и сам цвета темы не подхватит: перерисовка по смене темы, пока страница на экране.
        this.themes.Changed -= this.OnThemeChanged;
        this.themes.Changed += this.OnThemeChanged;

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
        this.themes.Changed -= this.OnThemeChanged;
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
        }

        base.OnDisappearing();
        this.timer?.Stop();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => this.Dial.Invalidate();

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
