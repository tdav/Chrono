using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;

namespace Chrono.Tests;

/// <summary>
/// Строки приложения. Приложение достаёт многие из них по составному ключу («Alert_{kind}», «Tip_{проблема}») и при его отсутствии
/// молча подставляет имя значения или пустую строку, а русская культура без ключа молча показывает английский: оба случая видны только на экране.
/// </summary>
public sealed class ResourcesTests
{
    private static Dictionary<string, string> Strings(CultureInfo culture) =>
        AppResources.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, @"\{(\d+)").Select(m => m.Groups[1].Value).Distinct().Order().ToArray();

    [Fact]
    public void Ru_HasEveryNeutralKeyAndNothingExtra()
    {
        var neutral = Strings(CultureInfo.InvariantCulture);
        var ru = Strings(new CultureInfo("ru"));

        var missingInRu = neutral.Keys.Except(ru.Keys).Order().ToList();
        var extraInRu = ru.Keys.Except(neutral.Keys).Order().ToList();

        Assert.True(missingInRu.Count == 0, $"Нет в русских строках: {string.Join(", ", missingInRu)}");
        Assert.True(extraInRu.Count == 0, $"Нет в нейтральных строках: {string.Join(", ", extraInRu)}");
    }

    [Fact]
    public void Ru_UsesTheSamePlaceholdersAsNeutral()
    {
        // Лишний или пропавший {n} в одной из культур даёт FormatException или потерянное значение только на телефоне с этим языком.
        var neutral = Strings(CultureInfo.InvariantCulture);
        var ru = Strings(new CultureInfo("ru"));

        foreach (var (key, text) in neutral)
        {
            Assert.True(Placeholders(text).SequenceEqual(Placeholders(ru[key])), $"{key}: «{text}» и «{ru[key]}» — разные подстановки");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("ru")]
    public void ComposedKeys_ExistForEveryValueTheCodeLooksUp(string culture)
    {
        var strings = Strings(new CultureInfo(culture));
        var expected = new List<string>();

        expected.AddRange(Enum.GetNames<AlertKind>().Select(name => $"Alert_{name}"));
        expected.AddRange(Enum.GetNames<RepeatKind>().Select(name => $"Repeat_{name}"));
        expected.AddRange(Sounds.All.Select(id => $"Sound_{id}"));
        expected.AddRange(ItemIcons.Keys.Select(key => $"Icon_{key}"));
        expected.AddRange(Enum.GetNames<DayOfWeek>().Select(name => $"RingsDay_{name}"));
        expected.AddRange(["RingsDay_Today", "RingsDay_Tomorrow"]);

        // Экран «Надёжность» и подсказка в списке: заголовок и статус строки, текст подсказки по первой проблеме.
        foreach (var item in Enum.GetNames<ReliabilityItem>())
        {
            expected.AddRange([$"RelTitle_{item}", $"RelOk_{item}", $"RelProblem_{item}", $"Tip_{item}"]);
        }

        expected.Add("Tip_Scheduling");

        var missing = expected.Where(key => !strings.ContainsKey(key)).ToList();

        Assert.True(missing.Count == 0, $"Нет строк (культура «{culture}»): {string.Join(", ", missing)}");
    }
}
