using Chrono.Models;

namespace Chrono.Services;

/// <summary>Единственная граница с планировщиком ОС; реализация — в Platforms/*.</summary>
public interface IAlarmScheduler
{
    /// <summary>Привести расписание ОС к списку: включённые будущие — запланировать, остальные — снять.</summary>
    void Sync(IReadOnlyList<Alarm> alarms);

    /// <summary>Снять расписание удалённого будильника.</summary>
    void Cancel(Guid id);

    /// <summary>Остановить звучащий сигнал (звук, вибрацию, хвост уведомлений).</summary>
    void StopRinging(Guid id);
}

/// <summary>Воспроизведение встроенной мелодии (прослушивание в редакторе, сигнал на iOS на переднем плане).</summary>
public interface ISoundPlayer
{
    void Play(string soundId, bool loop);

    void Stop();
}

public enum ReliabilityItem
{
    Notifications,
    ExactAlarms,
    FullScreen,
    Battery,
    TimeSensitive,
}

public sealed record ReliabilityStatus(ReliabilityItem Item, bool Ok);

/// <summary>Проверки «Надёжность»: разрешения и настройки, без которых будильник может не сработать.</summary>
public interface IReliabilityChecks
{
    Task<IReadOnlyList<ReliabilityStatus>> GetStatusAsync();

    Task RequestPermissionsAsync();

    void Fix(ReliabilityItem item);
}
