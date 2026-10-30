using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class TimeSheetModelTests
{
    [Fact]
    public void Footnote_AsksTargetForTheTimeOnTheWheels()
    {
        using var ui = new UiCulture("ru-RU");
        var asked = new List<TimeOnly>();
        var sheet = new TimeSheetModel
        {
            Target = time =>
            {
                asked.Add(time);
                return DateTime.Now.AddDays(3).AddHours(2).AddMinutes(30);
            },
        };

        sheet.Load(new TimeOnly(7, 45));
        var footnote = sheet.Footnote;

        Assert.All(asked, time => Assert.Equal(new TimeOnly(7, 45), time));
        Assert.StartsWith("Сработает ", footnote);
        Assert.EndsWith("— через 3 д 2 ч.", footnote);
    }

    [Fact]
    public void Footnote_TargetInThePast_AsksToPickNewDate()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new TimeSheetModel { Target = _ => DateTime.Now.AddDays(-1) };

        Assert.Equal("Время этого будильника прошло. Выберите новую дату.", sheet.Footnote);
    }

    [Fact]
    public void Footnote_DefaultTarget_IsTheNearestOccurrence()
    {
        using var ui = new UiCulture("ru-RU");
        var sheet = new TimeSheetModel();
        sheet.Load(new TimeOnly(7, 45));

        // «Ближайшее» всегда в пределах суток: сегодня или завтра; месяц — по-русски, в родительном падеже.
        Assert.Matches(
            "^Сработает (сегодня|завтра), \\d+ (января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря), — через ",
            sheet.Footnote);
    }

    [Fact]
    public void Value_FollowsHourAndMinute()
    {
        var sheet = new TimeSheetModel { Hour = 6, Minute = 5 };

        Assert.Equal(new TimeOnly(6, 5), sheet.Value);
    }

    [Theory]
    [InlineData(nameof(TimeSheetModel.Hour))]
    [InlineData(nameof(TimeSheetModel.Minute))]
    public void WheelChange_NotifiesValueAndFootnote(string wheel)
    {
        var sheet = new TimeSheetModel();
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        if (wheel == nameof(TimeSheetModel.Hour))
        {
            sheet.Hour = 9;
        }
        else
        {
            sheet.Minute = 30;
        }

        Assert.Contains(nameof(TimeSheetModel.Value), raised);
        Assert.Contains(nameof(TimeSheetModel.Footnote), raised);
    }

    [Fact]
    public void Load_SameTime_StillRefreshesFootnote()
    {
        // Время то же, а дата или повтор записи могли смениться, пока шторка была закрыта.
        var sheet = new TimeSheetModel();
        sheet.Load(new TimeOnly(7, 45));
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sheet.Load(new TimeOnly(7, 45));

        Assert.Contains(nameof(TimeSheetModel.Footnote), raised);
    }

    [Fact]
    public void RefreshFootnote_NotifiesOnlyTheFootnote()
    {
        var sheet = new TimeSheetModel();
        var raised = new List<string?>();
        sheet.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sheet.RefreshFootnote();

        Assert.Equal([nameof(TimeSheetModel.Footnote)], raised.ToArray());
    }
}
