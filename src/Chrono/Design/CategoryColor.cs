using Chrono.Services;

namespace Chrono.Design;

/// <summary>
/// Цвет категории дела (DESIGN.md §2.2, cat1…cat6): <c>design:CategoryColor.Index="{Binding CategoryIndex}"</c> красит фон элемента
/// ролью Cat1…Cat6 через ресурсы приложения — цвет следует за темой, литералов цвета нет.
/// Индекс 0–5 даёт <see cref="DialLayout.ColorIndex"/>; отрицательный — цвет не задан.
/// </summary>
public static class CategoryColor
{
    // Начальное значение -1, а не 0: иначе привязка с индексом 0 не вызвала бы propertyChanged и первая категория осталась бы без цвета.
    public static readonly BindableProperty IndexProperty = BindableProperty.CreateAttached(
        "Index",
        typeof(int),
        typeof(CategoryColor),
        -1,
        propertyChanged: (bindable, _, index) =>
        {
            if (bindable is VisualElement element && (int)index >= 0)
            {
                element.SetDynamicResource(VisualElement.BackgroundColorProperty, DialLayout.CategoryKey((int)index));
            }
        });

    // Символ на круге цвета категории: роль OnCat1…OnCat6 — белый, если даёт ≥ 3:1, иначе чёрный (оранжевая и бирюзовая
    // категории, «Тёмная · контраст»): <c>design:CategoryColor.GlyphIndex="{Binding CategoryIndex}"</c> на значке.
    public static readonly BindableProperty GlyphIndexProperty = BindableProperty.CreateAttached(
        "GlyphIndex",
        typeof(int),
        typeof(CategoryColor),
        -1,
        propertyChanged: (bindable, _, index) =>
        {
            if (bindable is Element element && (int)index >= 0)
            {
                element.SetDynamicResource(IconTint.ColorProperty, "On" + DialLayout.CategoryKey((int)index));
            }
        });

    public static int GetIndex(BindableObject view) => (int)view.GetValue(IndexProperty);

    public static void SetIndex(BindableObject view, int value) => view.SetValue(IndexProperty, value);

    public static int GetGlyphIndex(BindableObject view) => (int)view.GetValue(GlyphIndexProperty);

    public static void SetGlyphIndex(BindableObject view, int value) => view.SetValue(GlyphIndexProperty, value);
}
