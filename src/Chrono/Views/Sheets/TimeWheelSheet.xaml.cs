using Chrono.Services;
using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

public partial class TimeWheelSheet : ContentView
{
    private readonly ISoundPlayer? sound = IPlatformApplication.Current?.Services.GetService<ISoundPlayer>();
    private TimeSheetModel? model;
    private IDispatcherTimer? settleTimer;

    public TimeWheelSheet()
    {
        this.InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (this.model is not null)
        {
            this.model.ScrollRequested -= this.OnScrollRequested;
        }

        this.model = this.BindingContext as TimeSheetModel;
        if (this.model is not null)
        {
            this.model.ScrollRequested += this.OnScrollRequested;

            // Шторка создаётся при первом открытии, уже после Load: его запрос прокрутки ушёл без подписчика.
            this.OnScrollRequested(this, EventArgs.Empty);
        }
    }

    private void OnScrollRequested(object? sender, EventArgs e)
    {
        // Шторка только что стала видимой: список ещё не разложен, прокрутка до раскладки игнорируется.
        // Цель берём сейчас: Scrolled от первой раскладки может успеть переписать Hour/Minute до прокрутки.
        var hour = this.model!.Hour;
        var minute = this.model.Minute;
        this.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
        {
            this.HourWheel.ScrollTo(hour, position: ScrollToPosition.Center, animate: false);
            this.MinuteWheel.ScrollTo(minute, position: ScrollToPosition.Center, animate: false);
        });
    }

    private void OnWheelScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (this.model is null || sender is not CollectionView wheel)
        {
            return;
        }

        // VerticalOffset на Android копит только пользовательскую прокрутку (программный ScrollTo не учитывает),
        // а CenterItemIndex ошибается на строку — поэтому на Android центр берётся по геометрии списка.
        var index = e.CenterItemIndex;

        // Щелчок — только когда барабан крутит палец или он докатывается; программный ScrollTo при открытии
        // шторки прыгает без прокрутки (состояние Idle) и щёлкать не должен.
        var userScroll = true;
#if ANDROID
        if (wheel.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView list
            && list.FindChildViewUnder(list.Width / 2f, list.Height / 2f) is { } center)
        {
            userScroll = list.ScrollState != AndroidX.RecyclerView.Widget.RecyclerView.ScrollStateIdle;

            // Позиция 0 в адаптере — заголовок-отступ; NO_POSITION (−1) бывает во время анимации адаптера.
            var position = list.GetChildAdapterPosition(center);
            if (position < 0)
            {
                return;
            }

            index = position - 1;
        }
#endif
        // Доводка до строки: после флинга прилипание MandatorySingle на Android иногда не срабатывает
        // и барабан замирает между строками. Через 200 мс тишины докручиваем оба барабана к значениям модели.
        if (this.settleTimer is null)
        {
            this.settleTimer = this.Dispatcher.CreateTimer();
            this.settleTimer.Interval = TimeSpan.FromMilliseconds(200);
            this.settleTimer.IsRepeating = false;
            this.settleTimer.Tick += (_, _) =>
            {
#if ANDROID
                // Палец держит барабан или список ещё докатывается: доводка подождёт следующей паузы.
                if (this.HourWheel.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView hours
                    && this.MinuteWheel.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView minutes
                    && (hours.ScrollState != AndroidX.RecyclerView.Widget.RecyclerView.ScrollStateIdle
                        || minutes.ScrollState != AndroidX.RecyclerView.Widget.RecyclerView.ScrollStateIdle))
                {
                    this.settleTimer.Start();
                    return;
                }
#endif
                if (this.model is not null)
                {
                    this.HourWheel.ScrollTo(this.model.Hour, position: ScrollToPosition.Center, animate: true);
                    this.MinuteWheel.ScrollTo(this.model.Minute, position: ScrollToPosition.Center, animate: true);
                }
            };
        }

        this.settleTimer.Stop();
        this.settleTimer.Start();

        if (wheel == this.HourWheel)
        {
            index = Math.Clamp(index, 0, 23);
            if (this.model.Hour != index)
            {
                this.model.Hour = index;
                if (userScroll)
                {
                    this.sound?.Tick();
                }
            }
        }
        else
        {
            index = Math.Clamp(index, 0, 59);
            if (this.model.Minute != index)
            {
                this.model.Minute = index;
                if (userScroll)
                {
                    this.sound?.Tick();
                }
            }
        }
    }

    // Нажатие на строку барабана или на быстрое время («06:30», «Сейчас»): значение меняется прыжком, без прокрутки.
    private void OnTick(object? sender, EventArgs e) => this.sound?.Tick();
}
