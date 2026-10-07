using Chrono.Services;

namespace Chrono.Tests;

public sealed class SleepPhaseEstimatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 6, 10, 0, TimeSpan.Zero);

    private static readonly int[] Still = [0, 0, 1, 0, 0];

    private static IReadOnlyList<HeartRateSample> Pulse(params (int MinutesAgo, double Bpm)[] samples) =>
        samples.Select(s => new HeartRateSample(Now.AddMinutes(-s.MinutesAgo), s.Bpm)).ToList();

    [Theory]
    [InlineData(WatchSleepStage.Light, SleepPhase.Light)]
    [InlineData(WatchSleepStage.Rem, SleepPhase.Light)]
    [InlineData(WatchSleepStage.Awake, SleepPhase.Light)]
    [InlineData(WatchSleepStage.Deep, SleepPhase.Deep)]
    public void Estimate_WatchStage_WinsOverOtherSignals(WatchSleepStage stage, SleepPhase expected)
    {
        // Движение и высокий пульс противоречат «Deep», но готовая стадия часов главнее.
        var phase = SleepPhaseEstimator.Estimate(Now, stage, [9, 9, 9], Pulse((1, 90), (2, 90)), 55);

        Assert.Equal(expected, phase);
    }

    [Fact]
    public void Estimate_MovementInLastMinutes_IsLight()
    {
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, [0, 0, 0, SleepPhaseEstimator.MovementThreshold], [], null);

        Assert.Equal(SleepPhase.Light, phase);
    }

    [Fact]
    public void Estimate_MovementOlderThanLastEpochs_IsIgnored()
    {
        // Движение было 4 минуты назад, а проверяются только последние 3.
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, [9, 0, 0, 0], [], null);

        Assert.Equal(SleepPhase.Unknown, phase);
    }

    [Fact]
    public void Estimate_FreshPulseAboveBaseline_IsLight()
    {
        // База 55, порог подъёма max(3, 5% от 55 = 2,75) = 3 → нужно ≥ 58.
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((1, 58), (3, 59), (6, 58)), 55);

        Assert.Equal(SleepPhase.Light, phase);
    }

    [Fact]
    public void Estimate_FreshPulseJustBelowThreshold_IsDeep()
    {
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((1, 57.9), (3, 57.9)), 55);

        Assert.Equal(SleepPhase.Deep, phase);
    }

    [Fact]
    public void Estimate_RiseThresholdScalesWithHighBaseline()
    {
        // База 80: 5% = 4 > 3, поэтому +3.5 ещё не лёгкий сон.
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((1, 83.5), (2, 83.5)), 80);

        Assert.Equal(SleepPhase.Deep, phase);
    }

    [Fact]
    public void Estimate_SingleSpikeDoesNotLiftMedian()
    {
        // Один выброс 90 среди ровных 55 — медиана остаётся 55.
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((1, 55), (2, 90), (3, 55)), 55);

        Assert.Equal(SleepPhase.Deep, phase);
    }

    [Fact]
    public void Estimate_StalePulse_IsUnknown()
    {
        // Часы синхронизировали пульс 11 и 15 минут назад — для текущей фазы это уже устарело.
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((11, 70), (15, 70)), 55);

        Assert.Equal(SleepPhase.Unknown, phase);
    }

    [Fact]
    public void Estimate_FuturePulseSamples_AreIgnored()
    {
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((-1, 70), (-2, 70)), 55);

        Assert.Equal(SleepPhase.Unknown, phase);
    }

    [Fact]
    public void Estimate_NoBaseline_IsUnknown()
    {
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((1, 70), (2, 70)), null);

        Assert.Equal(SleepPhase.Unknown, phase);
    }

    [Fact]
    public void Estimate_SingleFreshSample_IsUnknown()
    {
        var phase = SleepPhaseEstimator.Estimate(Now, WatchSleepStage.None, Still, Pulse((1, 70)), 55);

        Assert.Equal(SleepPhase.Unknown, phase);
    }

    [Fact]
    public void Baseline_IsMedian_AndNeedsEnoughSamples()
    {
        Assert.Null(SleepPhaseEstimator.Baseline(Pulse((1, 50), (2, 60), (3, 70), (4, 80))));
        Assert.Equal(60, SleepPhaseEstimator.Baseline(Pulse((1, 50), (2, 60), (3, 99), (4, 52), (5, 61))));
        Assert.Equal(56.5, SleepPhaseEstimator.Baseline(Pulse((1, 50), (2, 60), (3, 99), (4, 52), (5, 61), (6, 53))));
    }

    [Theory]
    [InlineData(0, 0, 9.81, false)] // телефон лежит, шум датчика
    [InlineData(0.1, 0.1, 9.85, false)]
    [InlineData(0, 0, 10.3, true)] // толчок вдоль оси
    [InlineData(1.5, 0, 9.69, true)] // поворот: модуль почти тот же (≈9,81), но вектор сместился
    public void IsMovement_ComparesWithPreviousVector(double x, double y, double z, bool expected)
    {
        Assert.Equal(expected, SleepPhaseEstimator.IsMovement(0, 0, 9.81, x, y, z));
    }
}
