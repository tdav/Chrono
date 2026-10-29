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

    // Красный «−» режима «Изменить» открывает кнопку «Удалить» своей строки — как свайп влево.
    private void OnMinusTapped(object? sender, TappedEventArgs e)
    {
        for (var parent = (sender as Element)?.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is SwipeView swipe)
            {
                swipe.Open(OpenSwipeItem.RightItems);
                return;
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Chrono.Services.StartTrace.Mark("AlarmListPage: показана");

        // Список читаем сразу, до await: AppearingAsync может уйти в навигацию или проверки надёжности,
        // а вкладка не должна показывать данные, прочитанные до правок на «Распорядке».
        this.viewModel.Load();
        await this.viewModel.AppearingAsync();
        Chrono.Services.StartTrace.Mark("AlarmListPage: AppearingAsync выполнен");
    }
}
