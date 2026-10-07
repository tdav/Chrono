using Chrono.Models;
using Chrono.Services;

namespace Chrono.Tests;

public sealed class AlarmStoreTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chrono-store-");

    public void Dispose() => this.directory.Delete(recursive: true);

    [Fact]
    public void Load_MissingFile_ReturnsEmptyAndNotCorrupt()
    {
        var store = new AlarmStore(this.directory.FullName);

        var (alarms, wasCorrupt) = store.Load();

        Assert.Empty(alarms);
        Assert.False(wasCorrupt);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFieldsAndWallClockTime()
    {
        var store = new AlarmStore(this.directory.FullName);
        var alarm = new Alarm
        {
            At = new DateTime(2026, 10, 7, 6, 30, 0),
            Label = "Подъём",
            SoundEnabled = false,
            SoundId = "chime",
            VibrationEnabled = true,
            IsEnabled = true,
        };

        store.Save([alarm]);
        var (alarms, wasCorrupt) = store.Load();

        Assert.False(wasCorrupt);
        var loaded = Assert.Single(alarms);
        Assert.Equal(alarm, loaded);
        // «Настенное» время не сдвигается при сериализации.
        Assert.Equal(new DateTime(2026, 10, 7, 6, 30, 0), loaded.At);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new AlarmStore(this.directory.FullName);

        store.Save([new Alarm { At = new DateTime(2026, 10, 7, 6, 30, 0) }]);

        Assert.Equal(["alarms.json"], this.directory.GetFiles().Select(f => f.Name));
    }

    [Fact]
    public void Load_CorruptFile_MovesItAsideAndReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(this.directory.FullName, "alarms.json"), "{ это не json");
        var store = new AlarmStore(this.directory.FullName);

        var (alarms, wasCorrupt) = store.Load();

        Assert.Empty(alarms);
        Assert.True(wasCorrupt);
        Assert.False(File.Exists(Path.Combine(this.directory.FullName, "alarms.json")));
        var corrupt = Assert.Single(this.directory.GetFiles("alarms.corrupt-*.json"));
        Assert.Equal("{ это не json", File.ReadAllText(corrupt.FullName));
    }
}
