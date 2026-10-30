using Chrono.Models;
using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class SoundSheetModelTests
{
    [Fact]
    public void Options_NoSoundFirstThenEveryMelodyWithTranslatedName()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new SoundSheetModel(new FakeSoundPlayer());

        Assert.Equal(["", .. Sounds.All], sheet.Options.Select(o => o.Id).ToArray());
        Assert.Equal("Без звука", sheet.Options[0].Name);

        // Название мелодии без строки в ресурсах превращается в её Id: тест ловит пропущенный ключ Sound_{id}.
        Assert.All(sheet.Options.Skip(1), o => Assert.NotEqual(o.Id, o.Name));
        Assert.Equal([false, false, false, false, false, true], sheet.Options.Select(o => o.IsLast).ToArray());
    }

    [Fact]
    public void Constructor_SelectsDefaultMelody()
    {
        var sheet = new SoundSheetModel(new FakeSoundPlayer());

        Assert.Equal(Sounds.Default, sheet.SelectedId);
        Assert.Equal(Sounds.Default, Assert.Single(sheet.Options, o => o.IsSelected).Id);
    }

    [Fact]
    public void Load_SoundDisabled_SelectsNoSound()
    {
        var sheet = new SoundSheetModel(new FakeSoundPlayer());

        sheet.Load(soundEnabled: false, "chime");

        Assert.Equal("", sheet.SelectedId);
        Assert.Equal("", Assert.Single(sheet.Options, o => o.IsSelected).Id);
    }

    [Fact]
    public void Load_SoundEnabled_SelectsStoredMelody()
    {
        var sheet = new SoundSheetModel(new FakeSoundPlayer());

        sheet.Load(soundEnabled: true, "chime");

        Assert.Equal("chime", sheet.SelectedId);
        Assert.Equal("chime", Assert.Single(sheet.Options, o => o.IsSelected).Id);
    }

    [Fact]
    public void SelectCommand_Melody_SelectsAndPlaysItOnce()
    {
        var player = new FakeSoundPlayer();
        var sheet = new SoundSheetModel(player);

        sheet.SelectCommand.Execute(sheet.Options.Single(o => o.Id == "rising"));

        Assert.Equal("rising", sheet.SelectedId);
        Assert.Equal(("rising", false), Assert.Single(player.Played));
        Assert.Equal(0, player.Stops);
    }

    [Fact]
    public void SelectCommand_NoSound_StopsPreviewAndPlaysNothing()
    {
        var player = new FakeSoundPlayer();
        var sheet = new SoundSheetModel(player);

        sheet.SelectCommand.Execute(sheet.Options[0]);

        Assert.Equal("", sheet.SelectedId);
        Assert.Empty(player.Played);
        Assert.Equal(1, player.Stops);
    }
}
