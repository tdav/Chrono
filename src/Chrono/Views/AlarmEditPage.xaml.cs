using Chrono.Controls;
using Chrono.Resources.Strings;
using Chrono.ViewModels;
using Chrono.Views.Sheets;

namespace Chrono.Views;

public partial class AlarmEditPage : ContentPage
{
    private readonly AlarmEditViewModel viewModel;
    private IDispatcherTimer? clockTimer;
    // Поле «Название» и меню звука создаются вместе с шаблоном шторки: ссылки приходят из их Loaded.
    private Entry? nameEntry;
    private PopupMenu? soundMenu;
    private PopupMenu? alertMenu;

    public AlarmEditPage(AlarmEditViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Страница — шторка на большом детенте: открывается вместе со страницей и закрывается её уходом.
        this.Sheet.IsOpen = true;

        // «Через сколько» в сноске под барабаном устаревает, пока экран открыт; раз в 30 секунд — RefreshClock дешёвый.
        // Таймер создаётся здесь: к показу страницы Dispatcher уже привязан к окну.
        if (this.clockTimer is null)
        {
            this.clockTimer = this.Dispatcher.CreateTimer();
            this.clockTimer.Interval = TimeSpan.FromSeconds(30);
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
        if (this.viewModel.IsLeaving)
        {
            return true;
        }

        // Порядок «Назад» (DESIGN.md §3.1): открытое меню (звук, оповещение), затем шаг шторки, затем сама шторка — с вопросом при правках.
        if (this.soundMenu?.HandleBack() == true || this.alertMenu?.HandleBack() == true || this.Sheet.HandleBack())
        {
            return true;
        }

        return base.OnBackButtonPressed();
    }

    // Строки шагов: шаг показывает модель формы (она уже загружена), а выбор уходит в форму сразу — «назад» только возвращает на форму.
    private void OnRepeatTapped(object? sender, TappedEventArgs e) =>
        this.PushStep(new RepeatSheet { BindingContext = this.viewModel.RepeatSheet }, AppResources.RepeatHeader);

    // Меню от строки «Оповещение»: выбор применяется в форму сразу (AlarmEditViewModel следит за AlertSheet.SelectedId).
    private void OnAlertTapped(object? sender, TappedEventArgs e)
    {
        if (this.alertMenu is { } menu && sender is View row)
        {
            this.nameEntry?.Unfocus();
            menu.Anchor = row;
            menu.IsOpen = true;
        }
    }

    // Иконки — без внешней прокрутки: сетка сама прокручивается и занимает всю свободную высоту шага до низа шторки.
    private void OnIconTapped(object? sender, TappedEventArgs e)
    {
        this.nameEntry?.Unfocus();
        this.Sheet.Push(
            new ContentView { Padding = new Thickness(0, 8, 0, 24), Content = new IconSheet { BindingContext = this.viewModel.IconSheet } },
            AppResources.RowIcon);
    }

    private void OnDurationTapped(object? sender, TappedEventArgs e) =>
        this.PushStep(new DurationSheet { BindingContext = this.viewModel.DurationSheet }, AppResources.RowDuration);

    // Меню от строки «Звук»: выбор мелодии проигрывает её один раз, «Без звука» останавливает прослушивание (SoundSheetModel).
    private void OnSoundTapped(object? sender, TappedEventArgs e)
    {
        if (this.soundMenu is { } menu && sender is View row)
        {
            this.nameEntry?.Unfocus();
            menu.Anchor = row;
            menu.IsOpen = true;
        }
    }

    private void OnNameLoaded(object? sender, EventArgs e) => this.nameEntry = (Entry)sender!;

    private void OnSoundMenuLoaded(object? sender, EventArgs e) => this.soundMenu = (PopupMenu)sender!;

    private void OnAlertMenuLoaded(object? sender, EventArgs e) => this.alertMenu = (PopupMenu)sender!;

    // Два вызывающих (повтор, длительность): шаг идёт в прокрутке (на низком экране список не должен обрезаться) с теми же отступами, что у формы.
    private void PushStep(View step, string title)
    {
        // Клавиатура после «Названия» уходит вместе с формой.
        this.nameEntry?.Unfocus();
        this.Sheet.Push(
            new ScrollView { Padding = new Thickness(0, 8, 0, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Never, Content = step },
            title);
    }
}
