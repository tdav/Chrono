using System.Globalization;
using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

/// <summary>Часы с фиксированным моментом; локальный пояс — UTC, чтобы «настенное» время совпадало с моментом.</summary>
public sealed class FixedTime(DateTime now) : TimeProvider
{
    public DateTime Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(this.Now, DateTimeKind.Utc));

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Запоминает вызовы планировщика; при ThrowOnSync = true бросает из Sync.</summary>
public sealed class FakeScheduler : IAlarmScheduler
{
    public List<IReadOnlyList<Alarm>> Synced { get; } = [];

    public List<Guid> Cancelled { get; } = [];

    public bool ThrowOnSync { get; set; }

    public void Sync(IReadOnlyList<Alarm> alarms)
    {
        if (this.ThrowOnSync)
        {
            throw new InvalidOperationException("планировщик недоступен");
        }

        this.Synced.Add(alarms.ToList());
    }

    public void Cancel(Guid id) => this.Cancelled.Add(id);

    public void StopRinging(Guid id)
    {
    }
}

/// <summary>Запоминает вызовы проигрывателя мелодий.</summary>
public sealed class FakeSoundPlayer : ISoundPlayer
{
    public List<(string SoundId, bool Loop)> Played { get; } = [];

    public int Stops { get; private set; }

    public void Play(string soundId, bool loop) => this.Played.Add((soundId, loop));

    public void Stop() => this.Stops++;

    public void Tick()
    {
    }
}

/// <summary>Переключает язык интерфейса на время теста: строки AppResources и названия дней и месяцев берутся из него.</summary>
public sealed class UiCulture : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentUICulture;

    public UiCulture(string name) => CultureInfo.CurrentUICulture = new CultureInfo(name);

    public void Dispose() => CultureInfo.CurrentUICulture = this.previous;
}
