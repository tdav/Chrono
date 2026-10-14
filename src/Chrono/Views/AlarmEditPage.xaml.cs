using Chrono.ViewModels;

namespace Chrono.Views;

public partial class AlarmEditPage : ContentPage
{
    private readonly AlarmEditViewModel viewModel;
    private IDispatcherTimer? clockTimer;

    public AlarmEditPage(AlarmEditViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Плашка («сегодня/завтра») и «через сколько» устаревают, пока экран открыт; раз в секунду —
        // RefreshClock дешёвый: уведомления уходят только при смене значения.
        // Таймер создаётся здесь: к показу страницы Dispatcher уже привязан к окну.
        if (this.clockTimer is null)
        {
            this.clockTimer = this.Dispatcher.CreateTimer();
            this.clockTimer.Interval = TimeSpan.FromSeconds(1);
            this.clockTimer.Tick += (_, _) => this.viewModel.RefreshClock();
        }

        this.clockTimer.Start();
    }

    protected override void OnDisappearing()
    {
        this.clockTimer?.Stop();

        // Прослушивание мелодии не должно продолжаться после ухода со страницы (в т.ч. системной кнопкой «Назад»).
        this.viewModel.StopPreview();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        // Уход со страницы уже идёт (сохранение, удаление или подтверждённый выход): повторная «Назад»
        // показала бы диалог над уходящей страницей и сделала бы второй GoToAsync("..").
        if (this.viewModel.BackCommand.IsRunning || this.viewModel.SaveCommand.IsRunning || this.viewModel.DeleteCommand.IsRunning)
        {
            return true;
        }

        if (this.viewModel.IsAnySheetOpen)
        {
            this.viewModel.CloseSheetCommand.Execute(null);
            return true;
        }

        if (this.viewModel.IsDirty)
        {
            this.viewModel.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
