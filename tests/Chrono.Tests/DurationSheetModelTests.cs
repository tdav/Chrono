using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class DurationSheetModelTests
{
    private static string[] SelectedIds(DurationSheetModel sheet) => sheet.Options.Where(o => o.IsSelected).Select(o => o.Id).ToArray();

    [Fact]
    public void Options_ArePresetsStartingWithNone()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new DurationSheetModel();

        Assert.Equal(["0", "15", "30", "45", "60", "90"], sheet.Options.Select(o => o.Id).ToArray());
        Assert.Equal(["Нет", "15 мин", "30 мин", "45 мин", "60 мин", "90 мин"], sheet.Options.Select(o => o.Name).ToArray());
        Assert.Equal([false, false, false, false, false, true], sheet.Options.Select(o => o.IsLast).ToArray());
    }

    [Fact]
    public void Constructor_NoneIsSelected()
    {
        Assert.Equal(["0"], SelectedIds(new DurationSheetModel()));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(32, 32)] // не кратная 5 длительность (из Google Календаря) остаётся как есть
    [InlineData(720, 720)]
    [InlineData(1000, 720)]
    public void Load_ClampsToZeroAndMax(int minutes, int expected)
    {
        var sheet = new DurationSheetModel();

        sheet.Load(minutes);

        Assert.Equal(expected, sheet.Minutes);
    }

    [Theory]
    [InlineData(0, "Нет")]
    [InlineData(35, "35 мин")]
    [InlineData(720, "720 мин")]
    public void MinutesText_IsSubtitleOfTheCustomRow(int minutes, string expected)
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new DurationSheetModel();

        sheet.Load(minutes);

        Assert.Equal(expected, sheet.MinutesText);
    }

    [Fact]
    public void Minutes_MarkPresetOnlyWhenValueMatches()
    {
        var sheet = new DurationSheetModel();

        sheet.Load(45);
        Assert.Equal(["45"], SelectedIds(sheet));

        sheet.Load(32);
        Assert.Empty(SelectedIds(sheet));

        sheet.Load(0);
        Assert.Equal(["0"], SelectedIds(sheet));
    }

    [Theory]
    [InlineData(32, 35, 30)] // первое нажатие приводит к кратному в сторону нажатия
    [InlineData(30, 35, 25)]
    [InlineData(5, 10, 5)]
    [InlineData(0, 5, 0)]
    [InlineData(715, 720, 710)]
    [InlineData(720, 720, 715)]
    public void StepperCommands_MoveByFiveFromMultiples(int minutes, int afterPlus, int afterMinus)
    {
        var sheet = new DurationSheetModel();
        sheet.Load(minutes);
        sheet.IncreaseCommand.Execute(null);
        Assert.Equal(afterPlus, sheet.Minutes);

        sheet.Load(minutes);
        sheet.DecreaseCommand.Execute(null);
        Assert.Equal(afterMinus, sheet.Minutes);
    }

    [Fact]
    public void StepperCommands_KeepPresetSelectionInSync()
    {
        var sheet = new DurationSheetModel();
        sheet.Load(30);
        Assert.Equal(["30"], SelectedIds(sheet));

        sheet.IncreaseCommand.Execute(null);
        Assert.Empty(SelectedIds(sheet));

        sheet.IncreaseCommand.Execute(null);
        sheet.IncreaseCommand.Execute(null);
        Assert.Equal(["45"], SelectedIds(sheet));
    }

    [Fact]
    public void StepperButtons_AreInactiveAtTheEdges()
    {
        var sheet = new DurationSheetModel();

        sheet.Load(5);
        Assert.False(sheet.CanDecrease);
        Assert.False(sheet.DecreaseCommand.CanExecute(null));
        Assert.True(sheet.IncreaseCommand.CanExecute(null));

        sheet.Load(720);
        Assert.False(sheet.CanIncrease);
        Assert.False(sheet.IncreaseCommand.CanExecute(null));
        Assert.True(sheet.DecreaseCommand.CanExecute(null));

        sheet.Load(35);
        Assert.True(sheet.DecreaseCommand.CanExecute(null));
        Assert.True(sheet.IncreaseCommand.CanExecute(null));
    }

    [Fact]
    public void Minutes_Changed_NotifiesStepperAndSubtitle()
    {
        // Кнопки степпера и подзаголовок привязаны: без уведомлений «−» осталась бы активной у границы, а строка показывала бы старое значение.
        var sheet = new DurationSheetModel();
        var raised = new List<string?>();
        var decreaseChanged = 0;
        var increaseChanged = 0;
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        sheet.DecreaseCommand.CanExecuteChanged += (_, _) => decreaseChanged++;
        sheet.IncreaseCommand.CanExecuteChanged += (_, _) => increaseChanged++;

        sheet.Load(35);

        Assert.Contains(nameof(DurationSheetModel.MinutesText), raised);
        Assert.Contains(nameof(DurationSheetModel.CanDecrease), raised);
        Assert.Contains(nameof(DurationSheetModel.CanIncrease), raised);
        Assert.True(decreaseChanged > 0);
        Assert.True(increaseChanged > 0);
    }

    [Fact]
    public void SelectPresetCommand_SetsMinutesFromOptionId()
    {
        var sheet = new DurationSheetModel();

        sheet.SelectPresetCommand.Execute(sheet.Options.Single(o => o.Id == "45"));
        Assert.Equal(45, sheet.Minutes);

        sheet.SelectPresetCommand.Execute(sheet.Options[0]);
        Assert.Equal(0, sheet.Minutes);
    }
}
