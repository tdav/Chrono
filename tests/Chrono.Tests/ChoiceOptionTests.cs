using Chrono.ViewModels;

namespace Chrono.Tests;

public sealed class ChoiceOptionTests
{
    [Theory]
    [InlineData("ru-RU", "Пн, выбрано")]
    [InlineData("en-US", "Пн, selected")]
    public void AccessibleName_Selected_IsReadWithSelectedMark(string culture, string expected)
    {
        using var ui = new UiCulture(culture);
        var option = new ChoiceOption("Monday", "Пн") { IsSelected = true };

        Assert.Equal(expected, option.AccessibleName);
    }

    [Fact]
    public void AccessibleName_NotSelected_IsJustTheName()
    {
        using var ui = new UiCulture("ru-RU");

        Assert.Equal("Пн", new ChoiceOption("Monday", "Пн").AccessibleName);
    }

    [Fact]
    public void AccessibleName_FollowsSelectionBothWays()
    {
        using var ui = new UiCulture("ru-RU");
        var option = new ChoiceOption("bell", "Колокол");

        option.IsSelected = true;
        Assert.Equal("Колокол, выбрано", option.AccessibleName);

        option.IsSelected = false;
        Assert.Equal("Колокол", option.AccessibleName);
    }

    [Fact]
    public void IsSelected_Changed_NotifiesAccessibleName()
    {
        // Привязка AutomationProperties.Name перечитывает имя только по уведомлению: без него TalkBack продолжит читать старое.
        var option = new ChoiceOption("bell", "Колокол");
        var raised = new List<string?>();
        option.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        option.IsSelected = true;

        Assert.Contains(nameof(ChoiceOption.AccessibleName), raised);
    }
}
