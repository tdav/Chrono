namespace Chrono.Models;

/// <summary>Разовый будильник.</summary>
public sealed record Alarm
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Локальное «настенное» время срабатывания (Kind = Unspecified или Local).</summary>
    public DateTime At { get; init; }

    public string Label { get; init; } = "";

    public bool SoundEnabled { get; init; } = true;

    public string SoundId { get; init; } = Sounds.Default;

    public bool VibrationEnabled { get; init; } = true;

    public bool IsEnabled { get; init; } = true;
}
