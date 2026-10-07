using Chrono.Services;

namespace Chrono.Platform;

/// <summary>
/// iOS: текущую фазу сна с Apple Watch можно получить только из нативного watchOS-приложения на Swift
/// (HealthKit не исполняет код приложения в фоне в момент будильника), поэтому функция недоступна.
/// </summary>
public sealed class SmartWake : ISmartWake
{
    public bool IsSupported => false;

    public Task<SmartWakeAccess> GetAccessAsync() => Task.FromResult(SmartWakeAccess.Unsupported);

    public Task<SmartWakeAccess> RequestAccessAsync() => Task.FromResult(SmartWakeAccess.Unsupported);
}
