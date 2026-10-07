using System.Text.Json;
using Chrono.Models;

namespace Chrono.Services;

/// <summary>
/// Хранение будильников в alarms.json. Запись атомарная: временный файл + File.Move.
/// Все вызовы — с главного потока (UI, receiver'ы и сервис Android работают на нём же).
/// </summary>
public sealed class AlarmStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string directory;
    private readonly string path;

    public AlarmStore(string directory)
    {
        this.directory = directory;
        this.path = Path.Combine(directory, "alarms.json");
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
            var alarms = JsonSerializer.Deserialize<List<Alarm>>(File.ReadAllText(this.path), JsonOptions);
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
        File.WriteAllText(tempPath, JsonSerializer.Serialize(alarms, JsonOptions));
        File.Move(tempPath, this.path, overwrite: true);
    }
}
