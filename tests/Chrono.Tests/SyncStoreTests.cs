using Chrono.Services;

namespace Chrono.Tests;

public sealed class SyncStoreTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-sync-");
    private readonly SyncStore store;

    public SyncStoreTests() => this.store = new SyncStore(this.directory.FullName);

    public void Dispose() => this.directory.Delete(recursive: true);

    [Fact]
    public void SyncStore_RoundTripAndCorruptFile()
    {
        var id = Guid.NewGuid();
        var changedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        Assert.Null(this.store.Load().CalendarId);

        this.store.Save(new SyncState
        {
            CalendarId = "cal",
            SyncToken = "tok",
            Email = "a@b.c",
            LastSyncUtc = changedAt,
            Pending = [new PendingChange(id, SyncOp.Delete, "ev1", changedAt)],
            IgnoredEventIds = ["ev2"],
        });

        var state = this.store.Load();
        Assert.Equal(("cal", "tok", "a@b.c", (DateTime?)changedAt), (state.CalendarId, state.SyncToken, state.Email, state.LastSyncUtc));
        Assert.Equal([new PendingChange(id, SyncOp.Delete, "ev1", changedAt)], state.Pending);
        Assert.Equal(["ev2"], state.IgnoredEventIds);

        File.WriteAllText(Path.Combine(this.directory.FullName, "sync.json"), "{ не json");

        var recovered = this.store.Load();
        Assert.Null(recovered.CalendarId);
        Assert.Empty(recovered.Pending);
    }

    [Fact]
    public void Load_CorruptFile_MovedToBadSoSaveDoesNotLoseIt()
    {
        var path = Path.Combine(this.directory.FullName, "sync.json");
        File.WriteAllText(path + ".bad", "старый брак");
        File.WriteAllText(path, "{ не json");

        Assert.Null(this.store.Load().CalendarId);
        this.store.Save(new SyncState { CalendarId = "cal" });

        Assert.Equal("{ не json", File.ReadAllText(path + ".bad"));
        Assert.Equal("cal", this.store.Load().CalendarId);
    }

    [Fact]
    public void Load_CorruptFile_CannotMoveToBad_ReturnsEmptyWithoutThrowing()
    {
        // На месте .bad каталог: File.Move бросает, но Load (а с ним Enqueue из сохранения записи) падать не должен.
        var path = Path.Combine(this.directory.FullName, "sync.json");
        Directory.CreateDirectory(path + ".bad");
        File.WriteAllText(path, "{ не json");

        Assert.Null(this.store.Load().CalendarId);
        this.store.Enqueue(new PendingChange(Guid.NewGuid(), SyncOp.Upsert, null, DateTime.UtcNow));
    }

    [Fact]
    public void Enqueue_ReplacesSameAlarm()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var at = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

        this.store.Enqueue(new PendingChange(id, SyncOp.Upsert, null, at));
        this.store.Enqueue(new PendingChange(other, SyncOp.Upsert, null, at));
        this.store.Enqueue(new PendingChange(id, SyncOp.Delete, "ev1", at.AddMinutes(1)));

        var pending = this.store.Load().Pending;
        Assert.Equal(2, pending.Count);
        Assert.Equal(new PendingChange(id, SyncOp.Delete, "ev1", at.AddMinutes(1)), Assert.Single(pending, p => p.AlarmId == id));
    }
}
