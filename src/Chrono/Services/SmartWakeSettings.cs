namespace Chrono.Services;

/// <summary>Настройка умного пробуждения. Читается и UI, и платформенным планировщиком (в т.ч. из receiver'ов).</summary>
public static class SmartWakeSettings
{
    private const string EnabledKey = "smart_wake_enabled";

    public static bool Enabled
    {
        get => Preferences.Default.Get(EnabledKey, false);
        set => Preferences.Default.Set(EnabledKey, value);
    }
}
