namespace Chrono.Services;

/// <summary>
/// Шаги степпера «Своё, мин» (DESIGN.md §3.8): от 5 до 720 минут с шагом 5. Длительность, не кратная 5
/// (например, из Google Календаря), показывается как есть, а первое нажатие приводит её к ближайшему кратному в сторону нажатия:
/// 32 → «+» даёт 35, «−» даёт 30.
/// </summary>
public static class DurationSteps
{
    public const int Min = 5;

    public const int Max = 720;

    public const int Step = 5;

    /// <summary>Ближайшее кратное 5 строго больше <paramref name="minutes"/>, но не больше <see cref="Max"/>.</summary>
    public static int Increase(int minutes) => Math.Min(Max, ((Math.Max(minutes, 0) / Step) + 1) * Step);

    /// <summary>Ближайшее кратное 5 строго меньше <paramref name="minutes"/>, но не меньше <see cref="Min"/>; значение не больше <see cref="Min"/> не меняется.</summary>
    public static int Decrease(int minutes) => minutes <= Min ? minutes : Math.Max(Min, (minutes - 1) / Step * Step);

    public static bool CanIncrease(int minutes) => minutes < Max;

    public static bool CanDecrease(int minutes) => minutes > Min;
}
