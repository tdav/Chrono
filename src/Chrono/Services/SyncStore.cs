using System.Text.Json;

namespace Chrono.Services;

public enum SyncOp
{
    Upsert,
    Delete,
}

/// <summary>Локальное изменение, ещё не отправленное в Google. GoogleEventId нужен для Delete, когда записи уже нет.</summary>
public sealed record PendingChange(Guid AlarmId, SyncOp Op, string? GoogleEventId, DateTime ChangedAtUtc);

/// <summary>Состояние синхронизации (sync.json).</summary>
public sealed record SyncState
{
    public string? CalendarId { get; init; }

    public string? SyncToken { get; init; }

    public string? Email { get; init; }

    public DateTime? LastSyncUtc { get; init; }

    public List<PendingChange> Pending { get; init; } = [];

    /// <summary>События Google, которые пользователь удалил в Chrono; повторно не загружаются.</summary>
    public List<string> IgnoredEventIds { get; init; } = [];
}

/// <summary>Хранение SyncState в sync.json рядом с alarms.json. Запись атомарная, как в AlarmStore; вызовы — с главного потока.</summary>
public sealed class SyncStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string path;

    public SyncStore(string directory) => this.path = Path.Combine(directory, "sync.json");

    /// <summary>Нет файла или он повреждён (тогда переименовывается в sync.json.bad) — пустое состояние (следующая синхронизация загрузит всё заново).</summary>
    public SyncState Load()
    {
        if (!File.Exists(this.path))
        {
            return new SyncState();
        }

        try
        {
            return JsonSerializer.Deserialize<SyncState>(File.ReadAllText(this.path), JsonOptions) ?? new SyncState();
        }
        catch (JsonException)
        {
            // Повреждённый файл не удаляем: следующий Save иначе затёр бы его без следа.
            File.Move(this.path, this.path + ".bad", overwrite: true);
            return new SyncState();
        }
    }

    public void Save(SyncState state)
    {
        var tempPath = this.path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(tempPath, this.path, overwrite: true);
    }

    /// <summary>Ставит изменение в очередь; прежний элемент той же записи заменяется (важно последнее состояние).</summary>
    public void Enqueue(PendingChange change)
    {
        var state = this.Load();
        state.Pending.RemoveAll(p => p.AlarmId == change.AlarmId);
        state.Pending.Add(change);
        this.Save(state);
    }
}
