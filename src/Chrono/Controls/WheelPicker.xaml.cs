using System.Globalization;
using Chrono.Design;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chrono.Controls;

/// <summary>
/// Столбец барабана (DESIGN.md §3.2): значения 0…<see cref="Count"/>−1 по кругу — после последнего снова первое.
/// Круг — это <see cref="Copies"/> копий значений подряд: пока барабан крутится, он идёт по списку, а после остановки
/// возвращается на среднюю копию (ряд тот же, глазом не заметно), поэтому края списка недостижимы.
/// Барабан прилипает рядом к центру, нажатие на число прокручивает к нему, на каждый ряд — щелчок и тактильный отклик.
/// <see cref="Value"/> двусторонний: прокрутка пишет в него, а извне барабан переходит к значению прыжком.
/// </summary>
public partial class WheelPicker : ContentView
{
    // Копий значений в списке (по плану — пять) и номер средней.
    private const int Copies = 5;
    private const int MiddleCopy = Copies / 2;

    public static readonly BindableProperty CountProperty = BindableProperty.Create(
        nameof(Count), typeof(int), typeof(WheelPicker), 0,
        propertyChanged: (b, _, _) => ((WheelPicker)b).Rebuild());

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(int), typeof(WheelPicker), 0, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((WheelPicker)b).OnValueChanged());

    private readonly ISoundPlayer? sound = IPlatformApplication.Current?.Services.GetService<ISoundPlayer>();
    private IReadOnlyList<WheelItem> items = [];
    private IDispatcherTimer? settleTimer;
    private int centerIndex = -1;
    private int jumpRequests;
    private bool aligned = true;
    private bool jumping;

    public WheelPicker()
    {
        this.InitializeComponent();

        // Показ шторки: к значению барабан переходит, когда список уже разложен.
        this.Loaded += (_, _) => this.RequestJump();
        this.Unloaded += (_, _) => this.settleTimer?.Stop();
    }

    /// <summary>Сколько значений на барабане (24 часа, 60 минут): значения — 0…Count−1.</summary>
    public int Count
    {
        get => (int)this.GetValue(CountProperty);
        set => this.SetValue(CountProperty, value);
    }

    /// <summary>Значение в полосе выбора.</summary>
    public int Value
    {
        get => (int)this.GetValue(ValueProperty);
        set => this.SetValue(ValueProperty, value);
    }

    private void Rebuild()
    {
        this.items = Enumerable.Range(0, Copies * this.Count).Select(i => new WheelItem(i, i % this.Count)).ToArray();
        this.centerIndex = -1;
        this.List.ItemsSource = this.items;
        this.RequestJump();
    }

    private void OnValueChanged()
    {
        // Значение поставила сама прокрутка: барабан уже там.
        if (this.centerIndex >= 0 && this.items[this.centerIndex].Value == this.Value)
        {
            return;
        }

        this.RequestJump();
    }

    // Прыжок на среднюю копию к Value. Шторка только что стала видимой, список ещё не разложен, а прокрутка до раскладки
    // игнорируется — поэтому прыжок отложен; цель берётся в момент прыжка.
    private void RequestJump()
    {
        this.jumpRequests++;
        this.jumping = true;
        this.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
        {
            if (this.items.Count > 0)
            {
                this.Jump(MiddleCopy * this.Count + Math.Clamp(this.Value, 0, this.Count - 1));
            }
        });
    }

    // Прыжок без анимации. События прокрутки от него приходят позже и с чужим центром (раскладка): пока прыжок не
    // закончился, они значение не перезаписывают и не щёлкают. Новый прыжок продлевает ожидание предыдущего.
    private void Jump(int index)
    {
        var request = ++this.jumpRequests;
        this.jumping = true;
        this.List.ScrollTo(index, position: ScrollToPosition.Center, animate: false);
        this.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), () => this.jumping = request != this.jumpRequests);
    }

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (this.items.Count == 0)
        {
            return;
        }

        // Щелчок — когда барабан крутит палец или он докатывается; прыжок (ScrollTo без анимации) идёт в состоянии Idle.
        // Центр на Android берётся по геометрии списка: CenterItemIndex ошибается на строку, а VerticalOffset копит только
        // пользовательскую прокрутку. На остальных платформах прилипание доверено системе.
        var index = e.CenterItemIndex;
        var userScroll = true;
        this.aligned = true;
#if ANDROID
        if (this.List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView list
            && list.FindChildViewUnder(list.Width / 2f, list.Height / 2f) is { } center)
        {
            userScroll = list.ScrollState != AndroidX.RecyclerView.Widget.RecyclerView.ScrollStateIdle;

            // Палец на барабане: прыжок, который не успел завершиться, больше не ждём.
            if (userScroll)
            {
                this.jumping = false;
            }

            // NO_POSITION (−1) бывает во время анимации адаптера.
            var position = list.GetChildAdapterPosition(center);
            if (position < 0)
            {
                return;
            }

            index = position;
            this.aligned = Math.Abs((center.Top + center.Bottom) / 2f - list.Height / 2f) <= 2f;
        }
#endif
        index = Math.Clamp(index, 0, this.items.Count - 1);
        this.SetCenter(index);

        // Остановка: через 200 мс тишины барабан возвращается на среднюю копию и доходит до центра ряда, если прилипание
        // на Android не сработало и он замер между рядами.
        if (this.settleTimer is null)
        {
            var timer = this.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
#if ANDROID
                // Палец держит барабан или список ещё докатывается: доводка подождёт следующей паузы.
                if (this.List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView list
                    && list.ScrollState != AndroidX.RecyclerView.Widget.RecyclerView.ScrollStateIdle)
                {
                    timer.Start();
                    return;
                }
#endif
                if (this.centerIndex < 0)
                {
                    return;
                }

                var middle = MiddleCopy * this.Count;
                if (this.centerIndex < middle || this.centerIndex >= middle + this.Count)
                {
                    this.Jump(middle + this.items[this.centerIndex].Value);
                }
                else if (!this.aligned)
                {
                    this.List.ScrollTo(this.centerIndex, position: ScrollToPosition.Center, animate: true);
                }
            };
            this.settleTimer = timer;
        }

        this.settleTimer.Stop();
        this.settleTimer.Start();

        var value = this.items[index].Value;
        if (value != this.Value && !this.jumping)
        {
            this.Value = value;
            if (userScroll)
            {
                this.sound?.Tick();
            }
        }
    }

    // Ряды вокруг прежнего и нового центра: у прежних вид гаснет, у новых проявляется.
    private void SetCenter(int index)
    {
        if (index == this.centerIndex)
        {
            return;
        }

        var previous = this.centerIndex;
        this.centerIndex = index;
        var low = Math.Min(previous < 0 ? index : previous, index) - WheelItem.FarRows;
        var high = Math.Max(previous, index) + WheelItem.FarRows;
        for (var i = Math.Max(low, 0); i <= Math.Min(high, this.items.Count - 1); i++)
        {
            this.items[i].Distance = Math.Min(Math.Abs(i - index), WheelItem.FarRows);
        }
    }

    // Нажатие на число: барабан катится к нему, значение меняется по пути ряд за рядом, на каждом ряду щелчок.
    // «Уменьшение движения» — прыжок без прокрутки.
    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: WheelItem item })
        {
            var reduceMotion = IPlatformApplication.Current?.Services.GetService<ThemeService>()?.ReduceMotion ?? false;
            this.List.ScrollTo(item.Index, position: ScrollToPosition.Center, animate: !reduceMotion);
        }
    }
}

/// <summary>Ряд барабана: значение и вид по расстоянию до центра — выбранный рисуется Label, соседние мельче и бледнее.</summary>
public sealed partial class WheelItem : ObservableObject
{
    /// <summary>Дальше этого расстояния (в рядах) вид ряда не меняется: он почти растворился.</summary>
    public const int FarRows = 3;

    public WheelItem(int index, int value)
    {
        this.Index = index;
        this.Value = value;
        this.Label = value.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Номер ряда в списке (копий значений много, значение повторяется).</summary>
    public int Index { get; }

    public int Value { get; }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCenter), nameof(Scale), nameof(Fade))]
    public partial int Distance { get; set; } = FarRows;

    public bool IsCenter => this.Distance == 0;

    // Соседние ряды: 0.92 и 0.84 (DESIGN.md §3.2).
    public double Scale => this.Distance switch { 0 => 1, 1 => 0.92, _ => 0.84 };

    // Края гаснут к фону: сами ряды прозрачнее, а не накладка цветом фона (под шторкой на среднем детенте — стекло).
    public double Fade => this.Distance switch { 0 or 1 => 1, 2 => 0.55, _ => 0.2 };
}
