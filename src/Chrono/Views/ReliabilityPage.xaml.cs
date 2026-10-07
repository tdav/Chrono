using Chrono.ViewModels;

namespace Chrono.Views;

public partial class ReliabilityPage : ContentPage
{
    private readonly ReliabilityViewModel viewModel;
    private Window? window;

    public ReliabilityPage(ReliabilityViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Возврат из системных настроек не вызывает OnAppearing — обновляемся и при возобновлении окна.
        // Page.Window у страницы, открытой через Shell, в OnAppearing ещё null (проверено на эмуляторе: NRE),
        // поэтому берём окно приложения.
        this.window = Application.Current?.Windows.FirstOrDefault();
        if (this.window is not null)
        {
            this.window.Resumed += this.OnWindowResumed;
        }

        await this.viewModel.RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
        }

        base.OnDisappearing();
    }

    private async void OnWindowResumed(object? sender, EventArgs e) => await this.viewModel.RefreshAsync();
}
