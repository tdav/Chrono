using Chrono.ViewModels;

namespace Chrono.Views;

public partial class AlarmListPage : ContentPage
{
    private readonly AlarmListViewModel viewModel;

    public AlarmListPage(AlarmListViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await this.viewModel.AppearingAsync();
    }
}
