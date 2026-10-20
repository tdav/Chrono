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

        // Список читаем сразу, до await: AppearingAsync может уйти в навигацию или проверки надёжности,
        // а вкладка не должна показывать данные, прочитанные до правок на «Распорядке».
        this.viewModel.Load();
        await this.viewModel.AppearingAsync();
    }
}
