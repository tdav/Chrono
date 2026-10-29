namespace Chrono.Design;

/// <summary>Начертание стиля текста (DESIGN.json › type › weight).</summary>
public enum TextWeight
{
    Light = 300,
    Regular = 400,
    Medium = 500,
    Semibold = 600,
    Bold = 700,
}

/// <summary>Текстовый стиль Apple, по кривой которого растёт размер при Dynamic Type.</summary>
public enum DynamicType
{
    LargeTitle,
    Title1,
    Title2,
    Title3,
    Headline,
    Body,
    Callout,
    Subheadline,
    Footnote,
    Caption1,
    Caption2,
}

/// <summary>Метрики стиля текста при размере текста Large (по умолчанию в iOS) — TextStyles.g.cs.</summary>
/// <param name="Size">Кегль, pt.</param>
/// <param name="LineHeight">Интерлиньяж, pt.</param>
/// <param name="Weight">Начертание.</param>
/// <param name="Tracking">Трекинг, em (доля кегля).</param>
/// <param name="Scale">Стиль Apple, по которому растёт размер.</param>
/// <param name="MaxSize">Потолок роста, pt; null — без потолка.</param>
/// <param name="Tabular">Табличные цифры: время и обратный отсчёт не прыгают по ширине.</param>
public sealed record TextMetrics(double Size, double LineHeight, TextWeight Weight, double Tracking, DynamicType Scale, double? MaxSize, bool Tabular);
