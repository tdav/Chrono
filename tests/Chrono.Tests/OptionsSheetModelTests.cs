using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class OptionsSheetModelTests
{
    private static OptionsSheetModel Sheet(params string[] ids) => new(ids.Select(id => new ChoiceOption(id, id)));

    [Fact]
    public void Constructor_MarksOnlyTheLastOptionAsLast()
    {
        var sheet = Sheet("None", "Notify", "Ring");

        Assert.Equal([false, false, true], sheet.Options.Select(o => o.IsLast).ToArray());
    }

    [Fact]
    public void Constructor_EmptyList_DoesNotFail()
    {
        Assert.Empty(Sheet().Options);
    }

    [Fact]
    public void Load_SelectsOnlyTheMatchingOption()
    {
        var sheet = Sheet("None", "Notify", "Ring");

        sheet.Load("Notify");

        Assert.Equal("Notify", sheet.SelectedId);
        Assert.Equal([false, true, false], sheet.Options.Select(o => o.IsSelected).ToArray());
    }

    [Fact]
    public void Load_UnknownId_SelectsNothing()
    {
        var sheet = Sheet("None", "Notify");
        sheet.Load("Notify");

        sheet.Load("Beep");

        Assert.All(sheet.Options, o => Assert.False(o.IsSelected));
    }

    [Fact]
    public void SelectCommand_MovesSelectionToTheTappedOption()
    {
        var sheet = Sheet("None", "Notify", "Ring");
        sheet.Load("None");

        sheet.SelectCommand.Execute(sheet.Options[2]);

        Assert.Equal("Ring", sheet.SelectedId);
        Assert.Equal([false, false, true], sheet.Options.Select(o => o.IsSelected).ToArray());
    }
}
