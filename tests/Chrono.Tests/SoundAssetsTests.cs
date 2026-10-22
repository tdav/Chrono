using System.Text;
using Chrono.Models;

namespace Chrono.Tests;

/// <summary>
/// Встроенные звуки лежат в Resources/Raw и открываются по имени (Android: Assets.OpenFd).
/// Пропавший tick.wav роняет конструктор SoundPlayer — и вместе с ним открытие редактора.
/// </summary>
public sealed class SoundAssetsTests
{
    public static TheoryData<string> AllIds => new(Sounds.All.Concat(["beep", "tick"]));

    private static string RawPath(string id)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Chrono.slnx")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Корень репозитория (Chrono.slnx) не найден.");
        }

        return Path.Combine(dir.FullName, "src", "Chrono", "Resources", "Raw", $"{id}.wav");
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public void Sound_IsPcm16MonoWav(string id)
    {
        var bytes = File.ReadAllBytes(RawPath(id));

        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 20));  // PCM
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 22));  // моно
        Assert.Equal(16, BitConverter.ToUInt16(bytes, 34)); // 16 бит
    }

    [Fact]
    public void Tick_IsShorterThan50Ms()
    {
        // Щелчок звучит на каждую строку барабана: длинный при быстрой прокрутке сливается в гул.
        var bytes = File.ReadAllBytes(RawPath("tick"));
        var rate = BitConverter.ToInt32(bytes, 24);
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));
        var dataBytes = BitConverter.ToInt32(bytes, 40);

        Assert.InRange(dataBytes / 2.0 / rate, 0.001, 0.05);
    }
}
