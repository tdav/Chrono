namespace Chrono.Services;

public enum SleepPhase
{
    Unknown,
    Deep,
    Light,
}

/// <summary>Стадия сна из записи часов (Health Connect), если запись покрывает текущий момент.</summary>
public enum WatchSleepStage
{
    None,
    Awake,
    Light,
    Deep,
    Rem,
}

public readonly record struct HeartRateSample(DateTimeOffset Time, double Bpm);

/// <summary>
/// Оценка текущей фазы сна в окне умного пробуждения. Часы не отдают фазу в реальном времени,
/// поэтому она выводится из свежих данных: стадии из Health Connect (если есть), движения телефона
/// и подъёма пульса с часов над ночной медианой. Пороги — стартовые значения, требуют калибровки
/// на живых ночах (см. docs/superpowers/research/2026-10-07-smart-wake-sleep-phase.md, раздел 8).
/// </summary>
public static class SleepPhaseEstimator
{
    /// <summary>Окно до времени будильника, в котором ищется лёгкий сон.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    /// <summary>Отсчёты пульса старше этого считаются устаревшими (часы синхронизируются пачками).</summary>
    public static readonly TimeSpan FreshHeartRate = TimeSpan.FromMinutes(10);

    /// <summary>Сколько последних минут проверяется на движение.</summary>
    public const int MovementEpochs = 3;

    /// <summary>Минута считается «с движением», если в ней столько отсчётов движения акселерометра.</summary>
    public const int MovementThreshold = 3;

    /// <summary>Изменение вектора ускорения между соседними отсчётами (м/с²), с которого это движение.</summary>
    public const double MovementAccelerationDelta = 0.35;

    /// <summary>Подъём пульса над медианой ночи: не меньше этого числа ударов…</summary>
    public const double HeartRateRiseBpm = 3;

    /// <summary>…и не меньше этой доли медианы.</summary>
    public const double HeartRateRiseRatio = 0.05;

    /// <summary>Минимум отсчётов для медианы ночи и для оценки по свежему пульсу.</summary>
    public const int MinBaselineSamples = 5;

    public const int MinFreshSamples = 2;

    /// <summary>
    /// Отсчёт акселерометра — движение, если вектор ускорения заметно изменился с прошлого отсчёта.
    /// Сравнение модуля с g не годится: при повороте телефона модуль почти не меняется.
    /// </summary>
    public static bool IsMovement(double previousX, double previousY, double previousZ, double x, double y, double z)
    {
        var dx = x - previousX;
        var dy = y - previousY;
        var dz = z - previousZ;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) > MovementAccelerationDelta;
    }

    /// <summary>Медиана пульса за ночь до окна; null, если отсчётов мало.</summary>
    public static double? Baseline(IEnumerable<HeartRateSample> night)
    {
        var values = night.Select(s => s.Bpm).ToList();
        return values.Count < MinBaselineSamples ? null : Median(values);
    }

    /// <summary>
    /// Текущая фаза. Порядок признаков: стадия от часов → движение телефона → пульс с часов.
    /// Unknown — данных недостаточно (будильник тогда звонит в заданное время).
    /// </summary>
    /// <param name="movementPerMinute">Отсчёты движения по минутам, последняя минута — в конце.</param>
    public static SleepPhase Estimate(
        DateTimeOffset now,
        WatchSleepStage stage,
        IReadOnlyList<int> movementPerMinute,
        IReadOnlyList<HeartRateSample> heartRate,
        double? baselineBpm)
    {
        switch (stage)
        {
            // REM и бодрствование — не глубокий сон: пробуждение из них даёт меньше инерции сна.
            case WatchSleepStage.Light or WatchSleepStage.Rem or WatchSleepStage.Awake:
                return SleepPhase.Light;
            case WatchSleepStage.Deep:
                return SleepPhase.Deep;
        }

        // Заметное движение в глубоком сне редко: оно — признак лёгкого сна или пробуждения.
        if (movementPerMinute.TakeLast(MovementEpochs).Any(count => count >= MovementThreshold))
        {
            return SleepPhase.Light;
        }

        var fresh = heartRate.Where(s => s.Time > now - FreshHeartRate && s.Time <= now).ToList();
        if (baselineBpm is not double baseline || fresh.Count < MinFreshSamples)
        {
            return SleepPhase.Unknown;
        }

        var current = Median(fresh.Select(s => s.Bpm).ToList());
        var rise = Math.Max(HeartRateRiseBpm, baseline * HeartRateRiseRatio);

        // В глубоком сне пульс минимален и ровный; подъём над медианой ночи — выход из него.
        return current >= baseline + rise ? SleepPhase.Light : SleepPhase.Deep;
    }

    // Медиана нужна и для ночной базы, и для свежего окна; одиночный выброс пульса её не сдвигает.
    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }
}
