using Chrono.Services;
using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class CalendarSheetModelTests
{
    // «Сегодня» — четверг, 29 октября 2026: октябрь начинается с четверга, перед 1-м числом три пустые ячейки.
    private static readonly DateOnly Today = new(2026, 10, 29);

    private static CalendarSheetModel Loaded(DateOnly? selected = null, bool allowPast = false)
    {
        var sheet = new CalendarSheetModel();
        sheet.Load(selected, Today, allowPast);
        return sheet;
    }

    private static CalendarDayItem Day(CalendarSheetModel sheet, int day) => sheet.Cells.Single(c => c.Cell.Date?.Day == day);

    [Fact]
    public void Load_PlacesCellsInWeekGridFromMonday()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded();

        Assert.Equal(34, sheet.Cells.Count);
        Assert.Equal([0, 1, 2], sheet.Cells.Take(3).Select(c => c.Column).ToArray());
        Assert.All(sheet.Cells.Take(3), c => Assert.Null(c.Cell.Date));

        // 1 октября — четверг; 5-го понедельник начинает вторую строку; 31-го суббота — последняя ячейка пятой строки.
        Assert.Equal((0, 3), (Day(sheet, 1).Row, Day(sheet, 1).Column));
        Assert.Equal((1, 0), (Day(sheet, 5).Row, Day(sheet, 5).Column));
        Assert.Equal((4, 5), (Day(sheet, 31).Row, Day(sheet, 31).Column));
    }

    [Fact]
    public void Load_TitlesTheMonthInNominativeWithCapital()
    {
        using var ui = new UiCulture("ru-RU");

        Assert.Equal("Октябрь 2026", Loaded().MonthTitle);
        Assert.Equal("Февраль 2027", Loaded(new DateOnly(2027, 2, 14)).MonthTitle);
        Assert.Equal(["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"], Loaded().WeekDayNames.ToArray());
    }

    [Fact]
    public void Load_MarksOnlyTheSelectedDay()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(new DateOnly(2026, 10, 31));

        Assert.Equal(new DateOnly(2026, 10, 31), Assert.Single(sheet.Cells, c => c.IsSelected).Cell.Date);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Load_ClearButtonIsHiddenWhenPastDatesAreAllowed(bool allowPast, bool clearVisible)
    {
        var sheet = Loaded(allowPast: allowPast);

        Assert.Equal(clearVisible, sheet.IsClearVisible);
    }

    [Fact]
    public void ChipText_NoDate_ShowsTheEmptyText()
    {
        using var ui = new UiCulture("ru-RU");

        Assert.Equal("Ближайшее", Loaded().ChipText);
        Assert.Equal("Не выбрано", new CalendarSheetModel { EmptyChipText = "Не выбрано" }.ChipText);
    }

    [Fact]
    public void ChipText_CurrentYear_OmitsTheYear()
    {
        using var ui = new UiCulture("ru-RU");
        var chip = Loaded(new DateOnly(2026, 10, 31)).ChipText;

        Assert.StartsWith("сб, 31 окт", chip);
        Assert.DoesNotContain("2026", chip);
    }

    [Fact]
    public void ChipText_OtherYear_AddsTheYear()
    {
        using var ui = new UiCulture("ru-RU");
        var chip = Loaded(new DateOnly(2027, 1, 9)).ChipText;

        Assert.StartsWith("сб, 9 янв", chip);
        Assert.Contains("2027", chip);
    }

    [Fact]
    public void ChipText_Yearly_ShowsDayAndMonthOnly()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(new DateOnly(2026, 10, 17));
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sheet.IsYearly = true;

        Assert.Equal("17 октября", sheet.ChipText);
        Assert.Contains(nameof(CalendarSheetModel.ChipText), raised);
    }

    [Fact]
    public void Selected_Changed_NotifiesChipText()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded();
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sheet.PickCommand.Execute(Day(sheet, 30));

        Assert.Contains(nameof(CalendarSheetModel.ChipText), raised);
    }

    [Fact]
    public void PickCommand_SelectsTheDayAndMovesTheMark()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(new DateOnly(2026, 10, 30));

        sheet.PickCommand.Execute(Day(sheet, 31));

        Assert.Equal(new DateOnly(2026, 10, 31), sheet.Selected);
        Assert.Equal(31, Assert.Single(sheet.Cells, c => c.IsSelected).Cell.Date!.Value.Day);
    }

    [Fact]
    public void PickCommand_BlankOrPastCell_IsIgnored()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(new DateOnly(2026, 10, 30));

        sheet.PickCommand.Execute(sheet.Cells[0]);
        sheet.PickCommand.Execute(Day(sheet, 28));

        Assert.Equal(new DateOnly(2026, 10, 30), sheet.Selected);
    }

    [Fact]
    public void PickCommand_PastDay_IsAllowedWhenPastIsAllowed()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(allowPast: true);

        sheet.PickCommand.Execute(Day(sheet, 28));

        Assert.Equal(new DateOnly(2026, 10, 28), sheet.Selected);
    }

    [Fact]
    public void ClearCommand_ResetsSelectionAndMarks()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(new DateOnly(2026, 10, 31));

        sheet.ClearCommand.Execute(null);

        Assert.Null(sheet.Selected);
        Assert.All(sheet.Cells, c => Assert.False(c.IsSelected));
        Assert.Equal("Ближайшее", sheet.ChipText);
    }

    [Fact]
    public void TodayCommand_SelectsTodayAndReturnsToItsMonth()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = Loaded(new DateOnly(2027, 2, 14));

        sheet.TodayCommand.Execute(null);

        Assert.Equal(Today, sheet.Selected);
        Assert.Equal("Октябрь 2026", sheet.MonthTitle);
        Assert.Equal(Today, Assert.Single(sheet.Cells, c => c.IsSelected).Cell.Date);
    }

    [Fact]
    public void MonthCommands_RollOverTheYear()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new CalendarSheetModel();
        sheet.Load(null, new DateOnly(2026, 12, 15), allowPast: true);

        sheet.NextMonthCommand.Execute(null);
        Assert.Equal("Январь 2027", sheet.MonthTitle);

        sheet.PrevMonthCommand.Execute(null);
        sheet.PrevMonthCommand.Execute(null);
        Assert.Equal("Ноябрь 2026", sheet.MonthTitle);
    }

    [Theory]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(true, false, true, false, false, false)]
    [InlineData(false, true, true, false, false, true)]
    [InlineData(true, true, true, false, true, false)]
    public void DayItem_StateFlagsAreMutuallyExclusiveViews(
        bool isToday, bool isSelected, bool emphasized, bool plain, bool selectedToday, bool selectedOther)
    {
        var item = new CalendarDayItem(new CalendarCell(Today, false, isToday, false, true)) { IsSelected = isSelected };

        Assert.Equal(emphasized, item.IsEmphasized);
        Assert.Equal(plain, item.IsPlain);
        Assert.Equal(selectedToday, item.IsSelectedToday);
        Assert.Equal(selectedOther, item.IsSelectedOther);
    }

    [Fact]
    public void DayItem_Selected_NotifiesViewFlags()
    {
        var item = new CalendarDayItem(new CalendarCell(Today, false, false, false, true));
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.IsSelected = true;

        Assert.Contains(nameof(CalendarDayItem.IsEmphasized), raised);
        Assert.Contains(nameof(CalendarDayItem.IsPlain), raised);
        Assert.Contains(nameof(CalendarDayItem.IsSelectedToday), raised);
        Assert.Contains(nameof(CalendarDayItem.IsSelectedOther), raised);
    }

    [Fact]
    public void DayItem_DisabledOnlyForRealDaysThatAreNotEnabled()
    {
        var blank = new CalendarDayItem(new CalendarCell(null, false, false, false, false));
        var past = new CalendarDayItem(new CalendarCell(new DateOnly(2026, 10, 28), true, false, false, false));
        var open = new CalendarDayItem(new CalendarCell(Today, false, true, false, true));

        Assert.False(blank.IsDisabled);
        Assert.True(past.IsDisabled);
        Assert.False(open.IsDisabled);
    }
}
