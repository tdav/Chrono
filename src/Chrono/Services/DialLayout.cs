namespace Chrono.Services;

/// <summary>Половина суток на 12-часовом циферблате: Morning = [00:00, 12:00), Evening = [12:00, 24:00).</summary>
public enum DialHalf
{
    Morning,
    Evening,
}

/// <summary>
/// Срез записи на циферблате. Углы в градусах: 0 — «12 часов», по часовой стрелке, 30° на час.
/// У засечки (дело без длительности) SweepAngle = 0.
/// </summary>
public readonly record struct DialSlice(DayEntry Entry, double StartAngle, double SweepAngle, bool IsTick, int ColorIndex);

/// <summary>Расчёты вкладки «Циферблат» без зависимостей от MAUI: углы, обрезка по половинам суток, попадание касания, «текущее», «следующее», свободное окно.</summary>
public static class DialLayout
{
    /// <summary>Длительность новой записи по умолчанию, минут.</summary>
    public const int DefaultNewMinutes = 30;

    /// <summary>Внутренний край кольца секторов в долях радиуса обода (DESIGN.md §4.10): меньше — цифры и центр, касание ничего не выбирает.</summary>
    public const double InnerRadius = 0.70;

    /// <summary>Число категорий дел: цвета Cat1…Cat6 (DESIGN.md §2.2).</summary>
    public const int Categories = 6;

    /// <summary>Допуск касания засечки, градусов в каждую сторону.</summary>
    private const double TickTolerance = 4;

    /// <summary>Категория дела 0–5 из Id: сумма байтов по модулю 6 (GetHashCode между запусками нестабилен, а цвет дела не должен менять).</summary>
    public static int ColorIndex(Guid id) => id.ToByteArray().Sum(b => b) % Categories;

    /// <summary>Ключ цвета категории в ресурсах приложения: 0 → «Cat1», 5 → «Cat6»; значение раскладывает ThemeService по теме.</summary>
    public static string CategoryKey(int index) => $"Cat{index + 1}";

    public static DialHalf HalfOf(DateTime moment) => moment.Hour < 12 ? DialHalf.Morning : DialHalf.Evening;

    public static double AngleOf(TimeOnly time) => (time.Hour % 12 + time.Minute / 60.0) * 30;

    /// <summary>Срезы записей дня для одной половины суток. Порядок отрисовки: сначала длинные, последними короткие и засечки.</summary>
    public static IReadOnlyList<DialSlice> Slices(IReadOnlyList<DayEntry> entries, DateOnly day, DialHalf half)
    {
        var windowStart = day.ToDateTime(half == DialHalf.Morning ? TimeOnly.MinValue : new TimeOnly(12, 0));
        var windowEnd = windowStart.AddHours(12);
        var slices = new List<DialSlice>();

        foreach (var entry in entries)
        {
            var colorIndex = ColorIndex(entry.Alarm.Id);
            if (entry.Alarm.DurationMinutes <= 0)
            {
                if (entry.Start >= windowStart && entry.Start < windowEnd)
                {
                    slices.Add(new DialSlice(entry, AngleOf(TimeOnly.FromDateTime(entry.Start)), 0, true, colorIndex));
                }

                continue;
            }

            var from = entry.Start > windowStart ? entry.Start : windowStart;
            var to = entry.End < windowEnd ? entry.End : windowEnd;
            if (to <= from)
            {
                continue;
            }

            slices.Add(new DialSlice(entry, AngleOf(TimeOnly.FromDateTime(from)), (to - from).TotalHours * 30, false, colorIndex));
        }

        // «Короче — сверху» считается по полной длительности дела, а не по срезу после обрезки (у засечки 0).
        // OrderByDescending стабилен: при равной длительности сохраняется порядок записей.
        return slices.OrderByDescending(s => s.Entry.Alarm.DurationMinutes).ToList();
    }

    /// <summary>
    /// Запись под касанием. radiusFraction — расстояние от центра к радиусу обода; вне кольца [InnerRadius, 1.0] выбора нет.
    /// Из нескольких попаданий берётся самое короткое (засечка короче любого сектора), при равных — последнее в порядке отрисовки.
    /// Если выбранная запись selectedId сама под касанием и попаданий не меньше двух, повторное касание
    /// переходит к следующему попаданию в порядке отрисовки (после последнего — к первому), чтобы дотянуться до перекрытых засечек.
    /// </summary>
    public static DayEntry? HitTest(IReadOnlyList<DialSlice> slices, double angle, double radiusFraction, Guid? selectedId = null)
    {
        if (radiusFraction < InnerRadius || radiusFraction > 1.0)
        {
            return null;
        }

        var touch = (angle % 360 + 360) % 360;
        var hits = new List<DialSlice>();
        foreach (var slice in slices)
        {
            bool hit;
            if (slice.IsTick)
            {
                var delta = Math.Abs(touch - slice.StartAngle);
                hit = Math.Min(delta, 360 - delta) <= TickTolerance;
            }
            else
            {
                var offset = ((touch - slice.StartAngle) % 360 + 360) % 360;
                hit = offset < slice.SweepAngle;
            }

            if (hit)
            {
                hits.Add(slice);
            }
        }

        if (hits.Count == 0)
        {
            return null;
        }

        if (selectedId is { } id && hits.Count >= 2)
        {
            var index = hits.FindIndex(h => h.Entry.Alarm.Id == id);
            if (index >= 0)
            {
                return hits[(index + 1) % hits.Count].Entry;
            }
        }

        // <=: при равной длительности берётся последний в порядке отрисовки, то есть лежащий сверху.
        var best = hits[0];
        foreach (var hit in hits)
        {
            if (hit.Entry.Alarm.DurationMinutes <= best.Entry.Alarm.DurationMinutes)
            {
                best = hit;
            }
        }

        return best.Entry;
    }

    /// <summary>Идущее сейчас дело; при нескольких — самое короткое, при равных — последнее (верхнее, как в HitTest).</summary>
    public static DayEntry? Current(IReadOnlyList<DayEntry> entries, DateTime now)
    {
        DayEntry? best = null;
        foreach (var entry in entries)
        {
            if (entry.Alarm.DurationMinutes > 0 && entry.Start <= now && now < entry.End
                && (best is null || entry.Alarm.DurationMinutes <= best.Value.Alarm.DurationMinutes))
            {
                best = entry;
            }
        }

        return best;
    }

    /// <summary>Первая по порядку запись, начинающаяся после now.</summary>
    public static DayEntry? Next(IReadOnlyList<DayEntry> entries, DateTime now)
    {
        foreach (var entry in entries)
        {
            if (entry.Start > now)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Начало ближайшего свободного получасового окна: граница :00/:30 не раньше now, окно [граница, +30 мин) не пересекается
    /// с делами с длительностью. Если в сутках now свободной границы нет — ближайшая граница после now (после 23:30 это завтра).
    /// </summary>
    public static DateTime FreeSlot(IReadOnlyList<DayEntry> entries, DateTime now)
    {
        var halfHour = TimeSpan.FromMinutes(DefaultNewMinutes);
        var first = new DateTime((now.Ticks + halfHour.Ticks - 1) / halfHour.Ticks * halfHour.Ticks, now.Kind);

        for (var candidate = first; candidate.Date == now.Date; candidate += halfHour)
        {
            var end = candidate + halfHour;
            if (!entries.Any(e => e.Alarm.DurationMinutes > 0 && e.Start < end && e.End > candidate))
            {
                return candidate;
            }
        }

        return first;
    }
}
