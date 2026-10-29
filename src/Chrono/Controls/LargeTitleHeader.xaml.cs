namespace Chrono.Controls;

/// <summary>
/// Шапка корневого экрана вкладки: строка кнопок, под ней Large Title и подстрока. При прокрутке <see cref="ScrollView"/>
/// Large Title уходит вверх вместе с содержимым, а в строке кнопок по центру проявляется inline-заголовок.
/// Шапка лежит поверх прокручиваемого содержимого; страница ставит первым элементом содержимого отступ высотой <see cref="HeaderHeight"/>.
/// </summary>
public partial class LargeTitleHeader : ContentView
{
    private static readonly BindablePropertyKey HeaderHeightPropertyKey = BindableProperty.CreateReadOnly(
        nameof(HeaderHeight), typeof(double), typeof(LargeTitleHeader), 0.0);

    public static readonly BindableProperty HeaderHeightProperty = HeaderHeightPropertyKey.BindableProperty;

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(LargeTitleHeader), string.Empty);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(LargeTitleHeader), string.Empty,
        propertyChanged: (b, _, n) => ((LargeTitleHeader)b).SubtitleLabel.IsVisible = !string.IsNullOrEmpty((string?)n));

    public static readonly BindableProperty LeadingContentProperty = BindableProperty.Create(
        nameof(LeadingContent), typeof(View), typeof(LargeTitleHeader), null);

    public static readonly BindableProperty TrailingContentProperty = BindableProperty.Create(
        nameof(TrailingContent), typeof(View), typeof(LargeTitleHeader), null);

    public static readonly BindableProperty ScrollViewProperty = BindableProperty.Create(
        nameof(ScrollView), typeof(ScrollView), typeof(LargeTitleHeader), null,
        propertyChanged: (b, o, n) =>
        {
            var header = (LargeTitleHeader)b;
            if (o is ScrollView previous)
            {
                previous.Scrolled -= header.OnScrolled;
            }

            if (n is ScrollView current)
            {
                current.Scrolled += header.OnScrolled;
                header.UpdateScroll(current.ScrollY);
            }
        });

    public LargeTitleHeader()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => this.SetValue(HeaderHeightPropertyKey, this.Height);
    }

    /// <summary>Полная высота шапки: отступ, который страница оставляет сверху прокручиваемого содержимого.</summary>
    public double HeaderHeight => (double)this.GetValue(HeaderHeightProperty);

    public string Title
    {
        get => (string)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    /// <summary>Подстрока под Large Title («Ближайший — через 8 ч 15 мин»); пустая — строки нет.</summary>
    public string Subtitle
    {
        get => (string)this.GetValue(SubtitleProperty);
        set => this.SetValue(SubtitleProperty, value);
    }

    /// <summary>Элемент слева в строке кнопок («Изменить»).</summary>
    public View? LeadingContent
    {
        get => (View?)this.GetValue(LeadingContentProperty);
        set => this.SetValue(LeadingContentProperty, value);
    }

    /// <summary>Элемент справа в строке кнопок («+»).</summary>
    public View? TrailingContent
    {
        get => (View?)this.GetValue(TrailingContentProperty);
        set => this.SetValue(TrailingContentProperty, value);
    }

    /// <summary>Прокручиваемое содержимое страницы: по его смещению шапка сворачивается.</summary>
    public ScrollView? ScrollView
    {
        get => (ScrollView?)this.GetValue(ScrollViewProperty);
        set => this.SetValue(ScrollViewProperty, value);
    }

    /// <summary>
    /// Сворачивание по вертикальному смещению содержимого. Вызывается само по <see cref="ScrollView"/>; страница со списком
    /// (CollectionView) передаёт сюда VerticalOffset из его события Scrolled.
    /// </summary>
    public void UpdateScroll(double offset)
    {
        offset = Math.Max(offset, 0);
        this.TitleBlock.TranslationY = -offset;
        // Inline-заголовок проявляется за ход, равный высоте Large Title.
        var span = this.TitleLabel.Height;
        this.Bar.TitleOpacity = span > 0 ? Math.Clamp(offset / span, 0, 1) : (offset > 0 ? 1 : 0);
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e) => this.UpdateScroll(e.ScrollY);
}
