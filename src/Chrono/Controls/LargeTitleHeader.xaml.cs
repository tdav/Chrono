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
        propertyChanged: (b, _, _) => ((LargeTitleHeader)b).PlaceSubtitle());

    public static readonly BindableProperty SubtitleInBarProperty = BindableProperty.Create(
        nameof(SubtitleInBar), typeof(bool), typeof(LargeTitleHeader), false,
        propertyChanged: (b, _, _) => ((LargeTitleHeader)b).PlaceSubtitle());

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

    // Высоты шапки, измеренные на любой из вкладок: только с Large Title и с подстрокой под ним. Шапки всех вкладок одинаковы
    // по устройству, а высота из SizeChanged приходит лишь после первой раскладки: без запаса отступ на первом кадре новой
    // вкладки был 0, содержимое уходило под шапку и прыгало вниз.
    private static double titleOnlyHeight;
    private static double withSubtitleHeight;

    private bool measured;

    public LargeTitleHeader()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => this.OnSizeChanged();
        this.SubtitleLabel.SizeChanged += (_, _) => this.RememberHeights();
        this.PresetHeight();
    }

    private void OnSizeChanged()
    {
        if (this.Height <= 0)
        {
            return;
        }

        this.measured = true;
        this.SetValue(HeaderHeightPropertyKey, this.Height);
        this.RememberHeights();
    }

    // Шапка с подстрокой даёт обе высоты: без подстроки — за вычетом её строки. SizeChanged шапки приходит раньше, чем у подстроки
    // (её Height ещё -1), поэтому разность считается и по SizeChanged подстроки.
    private void RememberHeights()
    {
        if (!this.measured)
        {
            return;
        }

        if (!this.SubtitleLabel.IsVisible)
        {
            titleOnlyHeight = this.Height;
            return;
        }

        withSubtitleHeight = this.Height;
        if (this.SubtitleLabel.Height > 0)
        {
            titleOnlyHeight = this.Height - this.SubtitleLabel.Height;
        }
    }

    // Отступ до первой раскладки — по уже известной высоте шапки того же вида; после раскладки его задаёт SizeChanged.
    private void PresetHeight()
    {
        if (this.measured)
        {
            return;
        }

        var height = this.SubtitleLabel.IsVisible ? withSubtitleHeight : titleOnlyHeight;
        if (height > 0)
        {
            this.SetValue(HeaderHeightPropertyKey, height);
        }
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

    /// <summary>Подстрока — в строке кнопок по центру, а не под Large Title (решение пользователя для «Распорядка»).</summary>
    public bool SubtitleInBar
    {
        get => (bool)this.GetValue(SubtitleInBarProperty);
        set => this.SetValue(SubtitleInBarProperty, value);
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
        // Подстрока в строке кнопок уступает место inline-заголовку.
        this.BarSubtitle.Opacity = 1 - this.Bar.TitleOpacity;
    }

    // Два вызывающих (Subtitle и SubtitleInBar): подстрока показывается в одном из двух мест.
    private void PlaceSubtitle()
    {
        var has = !string.IsNullOrEmpty(this.Subtitle);
        this.SubtitleLabel.IsVisible = has && !this.SubtitleInBar;
        this.BarSubtitle.IsVisible = has && this.SubtitleInBar;
        this.PresetHeight();
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e) => this.UpdateScroll(e.ScrollY);
}
