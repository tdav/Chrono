using Chrono.Models;
using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class RepeatSheetModelTests
{
    [Fact]
    public void Options_FollowTheDocumentedOrderWithNames()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new RepeatSheetModel();

        // «Каждый месяц» стоит между «По дням недели» и «Каждый год», хотя в enum добавлен последним.
        Assert.Equal(["None", "Daily", "Weekly", "Monthly", "Yearly", "OnceThenDelete"], sheet.Options.Select(o => o.Id).ToArray());
        Assert.Equal(
            ["Однократно", "Каждый день", "По дням недели", "Каждый месяц", "Каждый год", "Один раз и удалить"],
            sheet.Options.Select(o => o.Name).ToArray());
        Assert.Equal([false, false, false, false, false, true], sheet.Options.Select(o => o.IsLast).ToArray());
    }

    [Fact]
    public void Days_StartOnMondayWithCapitalisedAbbreviations()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new RepeatSheetModel();

        Assert.Equal(
            ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
            sheet.Days.Select(d => d.Id).ToArray());
        Assert.Equal(["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"], sheet.Days.Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Load_Weekly_SelectsDaysAndSummarisesThem()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new RepeatSheetModel();

        sheet.Load(RepeatKind.Weekly, WeekDays.Friday | WeekDays.Monday | WeekDays.Wednesday);

        Assert.Equal(RepeatKind.Weekly, sheet.Repeat);
        Assert.True(sheet.IsDaysVisible);
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday, sheet.SelectedDays);
        Assert.Equal("Пн, Ср, Пт", sheet.DaysSummary);
        Assert.True(sheet.HasDays);
        Assert.False(sheet.IsDaysEmpty);
        Assert.Equal("Weekly", Assert.Single(sheet.Options, o => o.IsSelected).Id);
    }

    [Fact]
    public void Load_WeeklyWithoutDays_IsTheEmptyDaysError()
    {
        var sheet = new RepeatSheetModel();

        sheet.Load(RepeatKind.Weekly, WeekDays.None);

        Assert.False(sheet.HasDays);
        Assert.True(sheet.IsDaysEmpty);
        Assert.Equal("", sheet.DaysSummary);
    }

    [Fact]
    public void Load_DailyWithoutDays_IsNotAnError()
    {
        var sheet = new RepeatSheetModel();

        sheet.Load(RepeatKind.Daily, WeekDays.None);

        Assert.False(sheet.IsDaysVisible);
        Assert.False(sheet.IsDaysEmpty);
    }

    [Fact]
    public void Load_SameRepeatOtherDays_RaisesDaysNotifications()
    {
        // Повтор не изменился, поэтому его уведомление не придёт: сноска про дни обновляется отдельно.
        var sheet = new RepeatSheetModel();
        sheet.Load(RepeatKind.Weekly, WeekDays.Monday);
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sheet.Load(RepeatKind.Weekly, WeekDays.Tuesday | WeekDays.Wednesday);

        Assert.Contains(nameof(RepeatSheetModel.DaysSummary), raised);
        Assert.Contains(nameof(RepeatSheetModel.HasDays), raised);
        Assert.Contains(nameof(RepeatSheetModel.IsDaysEmpty), raised);
    }

    [Fact]
    public void ToggleDayCommand_FlipsTheDayAndTheEmptyError()
    {
        var sheet = new RepeatSheetModel();
        sheet.Load(RepeatKind.Weekly, WeekDays.None);
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sheet.ToggleDayCommand.Execute(sheet.Days[2]);

        Assert.Equal(WeekDays.Wednesday, sheet.SelectedDays);
        Assert.False(sheet.IsDaysEmpty);
        Assert.Contains(nameof(RepeatSheetModel.IsDaysEmpty), raised);

        sheet.ToggleDayCommand.Execute(sheet.Days[2]);

        Assert.Equal(WeekDays.None, sheet.SelectedDays);
        Assert.True(sheet.IsDaysEmpty);
    }

    [Fact]
    public void SelectCommand_ChangesRepeatAndShowsDaysOnlyForWeekly()
    {
        var sheet = new RepeatSheetModel();

        sheet.SelectCommand.Execute(sheet.Options.Single(o => o.Id == "Weekly"));
        Assert.Equal(RepeatKind.Weekly, sheet.Repeat);
        Assert.True(sheet.IsDaysVisible);

        sheet.SelectCommand.Execute(sheet.Options.Single(o => o.Id == "Monthly"));
        Assert.Equal(RepeatKind.Monthly, sheet.Repeat);
        Assert.False(sheet.IsDaysVisible);
        Assert.Equal("Monthly", Assert.Single(sheet.Options, o => o.IsSelected).Id);
    }
}
