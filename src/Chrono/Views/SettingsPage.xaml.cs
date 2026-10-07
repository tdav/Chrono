using Chrono.ViewModels;

namespace Chrono.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel viewModel;
    private Window? window;

    public SettingsPage(SettingsViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Возврат из экрана разрешений Health Connect или из Google Play не вызывает OnAppearing.
        // Page.Window у страницы Shell в OnAppearing ещё null — берём окно приложения.
        this.window = Application.Current?.Windows.FirstOrDefault();
        if (this.window is not null)
        {
            this.window.Resumed -= this.OnWindowResumed;
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
