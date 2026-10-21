using System.Text.Json;
using System.Text.Json.Serialization;
using Chrono.Models;

namespace Chrono.Services;

/// <summary>
/// Метаданные JSON, сгенерированные при компиляции. Отражение System.Text.Json на холодном старте стоило ≈0,3 с
/// главного потока при первом чтении alarms.json (RescheduleAll в конструкторе App); формат файла прежний.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IReadOnlyList<Alarm>))]
[JsonSerializable(typeof(List<Alarm>))]
[JsonSerializable(typeof(IReadOnlyDictionary<Guid, HashSet<DateOnly>>))]
[JsonSerializable(typeof(Dictionary<Guid, HashSet<DateOnly>>))]
internal sealed partial class AlarmStoreJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Хранение будильников в alarms.json и отметок «сделано» в done.json. Запись атомарная: временный файл + File.Move.
/// Все вызовы — с главного потока (UI, receiver'ы и сервис Android работают на нём же).
/// </summary>
public sealed class AlarmStore
{
    private readonly string directory;
    private readonly string path;
    private readonly string donePath;

    public AlarmStore(string directory)
    {
        this.directory = directory;
        this.path = Path.Combine(directory, "alarms.json");
        this.donePath = Path.Combine(directory, "done.json");
    }

    /// <summary>Читает список. Повреждённый файл переименовывается, возвращается пустой список и WasCorrupt = true.</summary>
    public (List<Alarm> Alarms, bool WasCorrupt) Load()
    {
        if (!File.Exists(this.path))
        {
            return ([], false);
        }

        try
        {
            var alarms = JsonSerializer.Deserialize(File.ReadAllText(this.path), AlarmStoreJsonContext.Default.ListAlarm);
            return (alarms ?? [], false);
        }
        catch (JsonException)
        {
            // Данные не теряем: откладываем файл в сторону для ручного разбора.
            var corruptPath = Path.Combine(this.directory, $"alarms.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(this.path, corruptPath, overwrite: true);
            return ([], true);
        }
    }

    public void Save(IReadOnlyList<Alarm> alarms)
    {
        var tempPath = this.path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(alarms, AlarmStoreJsonContext.Default.IReadOnlyListAlarm));
        File.Move(tempPath, this.path, overwrite: true);
    }

    /// <summary>Отметки «сделано» по записям и дням (done.json). Нет файла или он повреждён — пустой словарь.</summary>
    public Dictionary<Guid, HashSet<DateOnly>> LoadDone()
    {
        if (!File.Exists(this.donePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(this.donePath), AlarmStoreJsonContext.Default.DictionaryGuidHashSetDateOnly) ?? [];
        }
        catch (JsonException)
        {
            // Отметки некритичны: повреждённый файл просто считаем пустым.
            return [];
        }
    }

    public void SaveDone(IReadOnlyDictionary<Guid, HashSet<DateOnly>> done)
    {
        var tempPath = this.donePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(done, AlarmStoreJsonContext.Default.IReadOnlyDictionaryGuidHashSetDateOnly));
        File.Move(tempPath, this.donePath, overwrite: true);
    }
}
