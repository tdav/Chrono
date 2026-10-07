namespace Chrono.Services;

/// <summary>Настройки отключения сигнала. Читаются и экраном сигнала, и сервисом сигнала Android.</summary>
public static class DismissSettings
{
    private const string MathChallengeKey = "dismiss_math_challenge";

    /// <summary>Сигнал выключается только после верного решения примера с двузначными числами.</summary>
    public static bool MathChallenge
    {
        get => Preferences.Default.Get(MathChallengeKey, false);
        set => Preferences.Default.Set(MathChallengeKey, value);
    }
}
