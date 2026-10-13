using Chrono.ViewModels;

namespace Chrono.Views;

public partial class AlarmEditPage : ContentPage
{
    private readonly AlarmEditViewModel viewModel;
    private bool isShown;

    public AlarmEditPage(AlarmEditViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.isShown = true;

        // Плашка («сегодня/завтра») и «через сколько» устаревают, пока экран открыт.
        this.Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () =>
        {
            this.viewModel.RefreshClock();
            return this.isShown;
        });
    }

    protected override void OnDisappearing()
    {
        this.isShown = false;

        // Прослушивание мелодии не должно продолжаться после ухода со страницы (в т.ч. системной кнопкой «Назад»).
        this.viewModel.StopPreview();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
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
