using System.Collections.ObjectModel;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class SoundOption : ObservableObject
{
    public SoundOption(string id, string name)
    {
        this.Id = id;
        this.Name = name;
    }

    public string Id { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Создание и редактирование. Сохранение всегда включает будильник (как в системных «Часах»)
/// и требует время в будущем.
/// </summary>
public sealed partial class AlarmEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly ISoundPlayer soundPlayer;
    private Alarm? existing;

    public AlarmEditViewModel(AlarmService alarmService, ISoundPlayer soundPlayer)
    {
        this.alarmService = alarmService;
        this.soundPlayer = soundPlayer;
        this.SoundOptions = new(Sounds.All.Select(id =>
            new SoundOption(id, AppResources.ResourceManager.GetString($"Sound_{id}", AppResources.Culture) ?? id)));

        var start = DateTime.Now.AddHours(1);
        this.Hour = start.ToString("HH");
        this.Minute = start.ToString("mm");
        this.Day = start.ToString("dd");
        this.Month = start.ToString("MM");
        this.Year = start.ToString("yyyy");
        // Инициализатор свойства не вызывает OnSoundIdChanged — отмечаем мелодию по умолчанию явно.
        this.OnSoundIdChanged(this.SoundId);
    }

    public ObservableCollection<SoundOption> SoundOptions { get; }

    [ObservableProperty]
    public partial string Title { get; set; } = AppResources.NewAlarm;

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    // Время и дата вводятся цифрами в отдельные поля; разбираются в At.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(At), nameof(WhenText))]
    public partial string Hour { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(At), nameof(WhenText))]
    public partial string Minute { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(At), nameof(WhenText))]
    public partial string Day { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(At), nameof(WhenText))]
    public partial string Month { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(At), nameof(WhenText))]
    public partial string Year { get; set; } = "";

    /// <summary>Введённый момент или null, если поля не складываются в существующие дату и время.</summary>
    public DateTime? At =>
        int.TryParse(this.Hour, out var hour) && hour < 24
        && int.TryParse(this.Minute, out var minute) && minute < 60
        && int.TryParse(this.Day, out var day)
        && int.TryParse(this.Month, out var month) && month is >= 1 and <= 12
        && int.TryParse(this.Year, out var year) && year is >= 2000 and <= 9999
        && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateTime(year, month, day, hour, minute, 0)
            : null;

    /// <summary>Подсказка под полями: день недели и дата словами; пусто, пока ввод неполный (ошибку покажет сохранение).</summary>
    public string WhenText => this.At?.ToString("dddd, d MMMM yyyy") ?? "";

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string SoundId { get; set; } = Sounds.Default;

    [ObservableProperty]
    public partial bool VibrationEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorText { get; set; } = "";

    public bool HasError => !string.IsNullOrEmpty(this.ErrorText);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("id", out var value) || !Guid.TryParse(value?.ToString(), out var id))
        {
            return;
        }

        this.existing = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
        if (this.existing is null)
        {
            return;
        }

        this.Title = AppResources.EditAlarm;
        this.IsExisting = true;
        this.Hour = this.existing.At.ToString("HH");
        this.Minute = this.existing.At.ToString("mm");
        this.Day = this.existing.At.ToString("dd");
        this.Month = this.existing.At.ToString("MM");
        this.Year = this.existing.At.ToString("yyyy");
        this.Label = this.existing.Label;
        this.SoundEnabled = this.existing.SoundEnabled;
        this.VibrationEnabled = this.existing.VibrationEnabled;
        this.SoundId = this.existing.SoundId;

        if (query.ContainsKey("expired"))
        {
            this.ErrorText = AppResources.PickNewDate;
        }
    }

    [RelayCommand]
    private void SelectSound(SoundOption option)
    {
        this.SoundId = option.Id;
        this.soundPlayer.Play(option.Id, loop: false);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (this.At is not { } at)
        {
            this.ErrorText = AppResources.InvalidDateTime;
            return;
        }

        var alarm = (this.existing ?? new Alarm()) with
        {
            At = at,
            Label = this.Label.Trim(),
            SoundEnabled = this.SoundEnabled,
            SoundId = this.SoundId,
            VibrationEnabled = this.VibrationEnabled,
            IsEnabled = true,
        };

        if (!this.alarmService.Save(alarm))
        {
            this.ErrorText = AppResources.TimeInPast;
            return;
        }

        this.soundPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        this.soundPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (this.existing is not null)
        {
            this.alarmService.Delete(this.existing.Id);
        }

        this.soundPlayer.Stop();
        await Shell.Current.GoToAsync("..");
    }

    public void StopPreview() => this.soundPlayer.Stop();

    partial void OnHourChanged(string value) => this.ErrorText = "";

    partial void OnMinuteChanged(string value) => this.ErrorText = "";

    partial void OnDayChanged(string value) => this.ErrorText = "";

    partial void OnMonthChanged(string value) => this.ErrorText = "";

    partial void OnYearChanged(string value) => this.ErrorText = "";

    partial void OnSoundIdChanged(string value)
    {
        foreach (var option in this.SoundOptions)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
