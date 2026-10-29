using Chrono.Design;
using Chrono.Resources.Strings;

namespace Chrono.Controls;

/// <summary>
/// Сегменты (DESIGN.md §3): 2–5 вариантов одного свойства, равной ширины. <see cref="Items"/> — подписи, <see cref="SelectedIndex"/> —
/// выбранный (двусторонняя привязка). Бегунок переезжает пружиной 0.2 с; при «Уменьшении движения» — сразу.
/// </summary>
public partial class SegmentedControl : ContentView
{
    // Отступ бегунка от трека с каждой стороны; тот же отступ у сетки подписей (см. XAML), чтобы подпись стояла по центру бегунка.
    private const double Inset = 2;

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IList<string>), typeof(SegmentedControl), null,
        propertyChanged: (b, _, _) => ((SegmentedControl)b).OnItemsChanged());

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(SegmentedControl), 0, BindingMode.TwoWay,
        propertyChanged: (b, _, _) =>
        {
            var control = (SegmentedControl)b;
            control.UpdateLook();
            control.PlaceThumb(true);
        });

    private readonly List<Label> labels = [];
    private readonly List<Grid> cells = [];

    public SegmentedControl()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => this.PlaceThumb(false);
    }

    /// <summary>Подписи сегментов по порядку: «Сейчас», «Вечер».</summary>
    public IList<string>? Items
    {
        get => (IList<string>?)this.GetValue(ItemsProperty);
        set => this.SetValue(ItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)this.GetValue(SelectedIndexProperty);
        set => this.SetValue(SelectedIndexProperty, value);
    }

    // Подписи и жесты собираются заново при каждой смене Items.
    private void OnItemsChanged()
    {
        this.Cells.Children.Clear();
        this.Cells.ColumnDefinitions.Clear();
        this.labels.Clear();
        this.cells.Clear();

        var items = this.Items ?? [];
        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var label = new Label { Text = items[i] };
            // Область нажатия — вся ячейка высотой 44; прозрачный фон нужен, чтобы касание принимали и пустые места ячейки.
            var cell = new Grid { BackgroundColor = Colors.Transparent, Children = { label } };
            cell.SetValue(SemanticProperties.DescriptionProperty, items[i]);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => this.SelectedIndex = index;
            cell.GestureRecognizers.Add(tap);

            this.Cells.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            this.Cells.Add(cell, i);
            this.labels.Add(label);
            this.cells.Add(cell);
        }

        this.UpdateLook();
        this.PlaceThumb(false);
    }

    // Выбранная подпись — Semibold, остальные — Regular; у выбранной сегмента для чтения с экрана подсказка «Выбрано».
    private void UpdateLook()
    {
        for (var i = 0; i < this.labels.Count; i++)
        {
            var selected = i == this.SelectedIndex;
            this.labels[i].Style = (Style)this.Resources[selected ? "SegmentTextSelected" : "SegmentText"];
            this.cells[i].SetValue(SemanticProperties.HintProperty, selected ? AppResources.SegmentSelected : string.Empty);
        }
    }

    // Бегунок: ширина сегмента за вычетом отступов; сдвиг по индексу. Из SizeChanged и при первой сборке — без анимации.
    private void PlaceThumb(bool animate)
    {
        var count = this.labels.Count;
        if (count == 0 || this.Width <= 2 * Inset)
        {
            return;
        }

        var width = (this.Width - 2 * Inset) / count;
        var x = Math.Clamp(this.SelectedIndex, 0, count - 1) * width;
        this.Thumb.WidthRequest = width;
        this.Thumb.CancelAnimations();
        if (animate && this.Handler?.MauiContext?.Services.GetService<ThemeService>()?.ReduceMotion != true)
        {
            _ = this.Thumb.TranslateToAsync(x, 0, 200, Easing.SpringOut);
        }
        else
        {
            this.Thumb.TranslationX = x;
        }
    }
}
