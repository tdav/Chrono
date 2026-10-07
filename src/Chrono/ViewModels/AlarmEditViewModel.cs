using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Чип выбора: мелодия, вид повтора или день недели.</summary>
public sealed partial class ChoiceOption : ObservableObject
{
    public ChoiceOption(string id, string name)
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
            new ChoiceOption(id, AppResources.ResourceManager.GetString($"Sound_{id}", AppResources.Culture) ?? id)));
        this.RepeatOptions = new(Enum.GetValues<RepeatKind>().Select(kind =>
            new ChoiceOption(kind.ToString(), AppResources.ResourceManager.GetString($"Repeat_{kind}", AppResources.Culture) ?? kind.ToString())));
        // Дни — с первого дня недели культуры (в России с понедельника).
        var format = CultureInfo.CurrentUICulture.DateTimeFormat;
        this.DayOptions = new(Enumerable.Range(0, 7)
            .Select(i => (DayOfWeek)(((int)format.FirstDayOfWeek + i) % 7))
            .Select(day => new ChoiceOption(((WeekDays)(1 << (int)day)).ToString(), format.AbbreviatedDayNames[(int)day])));

        var start = DateTime.Now.AddHours(1);
        this.Hour = start.ToString("HH");
        this.Minute = start.ToString("mm");
        this.Day = start.ToString("dd");
        this.Month = start.ToString("MM");
        this.Year = start.ToString("yyyy");
        // Инициализатор свойства не вызывает OnSoundIdChanged — отмечаем мелодию по умолчанию явно.
        this.OnSoundIdChanged(this.SoundId);
        this.OnRepeatChanged(this.Repeat);
    }

    public ObservableCollection<ChoiceOption> SoundOptions { get; }

    public ObservableCollection<ChoiceOption> RepeatOptions { get; }

    public ObservableCollection<ChoiceOption> DayOptions { get; }

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WhenText), nameof(IsDateVisible), nameof(IsDaysVisible))]
    public partial RepeatKind Repeat { get; set; } = RepeatKind.None;

    /// <summary>Дата нужна разовому и ежегодному; ежедневному и по дням недели — только время.</summary>
    public bool IsDateVisible => this.Repeat is RepeatKind.None or RepeatKind.Yearly;

    public bool IsDaysVisible => this.Repeat == RepeatKind.Weekly;

    /// <summary>
    /// Подсказка под полями: дата словами, у повторяющегося — ближайшее срабатывание;
    /// пусто, пока ввод неполный (ошибку покажет сохранение).
    /// </summary>
    public string WhenText => this.At is not { } at
        ? ""
        : this.Repeat == RepeatKind.None
            ? at.ToString("dddd, d MMMM yyyy")
            : string.Format(
                AppResources.NextOccurrence,
                new Alarm { At = at, Repeat = this.Repeat, Days = this.SelectedDays }.NextAfter(DateTime.Now).ToString("dddd, d MMMM yyyy"));

    private WeekDays SelectedDays =>
        this.DayOptions.Where(o => o.IsSelected).Aggregate(WeekDays.None, (days, o) => days | Enum.Parse<WeekDays>(o.Id));

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
        this.Repeat = this.existing.Repeat;
        foreach (var option in this.DayOptions)
        {
            option.IsSelected = this.existing.Days.HasFlag(Enum.Parse<WeekDays>(option.Id));
        }

        if (query.ContainsKey("expired"))
        {
            this.ErrorText = AppResources.PickNewDate;
        }
    }

    [RelayCommand]
    private void SelectSound(ChoiceOption option)
    {
        this.SoundId = option.Id;
        this.soundPlayer.Play(option.Id, loop: false);
    }

    [RelayCommand]
    private void SelectRepeat(ChoiceOption option) => this.Repeat = Enum.Parse<RepeatKind>(option.Id);

    [RelayCommand]
    private void ToggleDay(ChoiceOption option)
    {
        option.IsSelected = !option.IsSelected;
        this.ErrorText = "";
        this.OnPropertyChanged(nameof(this.WhenText));
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
            Repeat = this.Repeat,
            Days = this.Repeat == RepeatKind.Weekly ? this.SelectedDays : WeekDays.None,
        };

        if (alarm.Repeat == RepeatKind.Weekly && alarm.Days == WeekDays.None)
        {
            this.ErrorText = AppResources.NoDaysSelected;
            return;
        }

        // У повторяющегося из полей берётся время (и день с месяцем для ежегодного), At — ближайшее срабатывание.
        if (alarm.Repeat != RepeatKind.None)
        {
            alarm = alarm with { At = alarm.NextAfter(DateTime.Now) };
        }

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

    partial void OnRepeatChanged(RepeatKind value)
    {
        this.ErrorText = "";
        foreach (var option in this.RepeatOptions)
        {
            option.IsSelected = option.Id == value.ToString();
        }
    }

    partial void OnSoundIdChanged(string value)
    {
        foreach (var option in this.SoundOptions)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
