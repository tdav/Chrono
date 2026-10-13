using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>
/// Создание и редактирование. Сохранение всегда включает будильник (как в системных «Часах»)
/// и требует время в будущем. Время, дата, повтор и звук выбираются в нижних шторках.
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
        this.SoundSheet = new SoundSheetModel(soundPlayer);

        var start = DateTime.Now.AddHours(1);
        this.Time = new TimeOnly(start.Hour, start.Minute);

        this.TimeSheet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TimeSheetModel.Hour) or nameof(TimeSheetModel.Minute))
            {
                this.OnPropertyChanged(nameof(this.TimeHint));
            }
        };

        this.TimeSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.Time = this.TimeSheet.Value;
            }

            this.IsTimeSheetOpen = false;
        };

        this.CalendarSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                // Ежегодному нужны день и месяц: «Ближайшее» для него не имеет смысла.
                this.Date = this.CalendarSheet.Selected ?? (this.Repeat == RepeatKind.Yearly ? DateOnly.FromDateTime(DateTime.Now) : null);
            }

            this.IsDateSheetOpen = false;
        };

        this.RepeatSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.Repeat = this.RepeatSheet.Repeat;
                this.Days = this.RepeatSheet.Repeat == RepeatKind.Weekly ? this.RepeatSheet.SelectedDays : WeekDays.None;

                // Ежегодному нужны день и месяц: «Ближайшее» для него не имеет смысла.
                if (this.Repeat == RepeatKind.Yearly && this.Date is null)
                {
                    this.Date = DateOnly.FromDateTime(DateTime.Now);
                }
            }

            this.IsRepeatSheetOpen = false;
        };

        this.SoundSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.SoundEnabled = this.SoundSheet.SelectedId.Length > 0;
                if (this.SoundEnabled)
                {
                    this.SoundId = this.SoundSheet.SelectedId;
                }
            }

            this.IsSoundSheetOpen = false;
        };

        // Начальные значения не считаются правками.
        this.IsDirty = false;
    }

    public TimeSheetModel TimeSheet { get; } = new();

    public CalendarSheetModel CalendarSheet { get; } = new();

    public RepeatSheetModel RepeatSheet { get; } = new();

    public SoundSheetModel SoundSheet { get; }

    [ObservableProperty]
    public partial bool IsTimeSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsDateSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsRepeatSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsSoundSheetOpen { get; set; }

    public bool IsAnySheetOpen => this.IsTimeSheetOpen || this.IsDateSheetOpen || this.IsRepeatSheetOpen || this.IsSoundSheetOpen;

    [ObservableProperty]
    public partial string Title { get; set; } = AppResources.NewAlarm;

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText), nameof(PlateTitle), nameof(RelText))]
    public partial TimeOnly Time { get; set; }

    /// <summary>null — «Ближайшее» (сегодня или завтра по времени).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText), nameof(PlateTitle), nameof(RelText), nameof(TimeHint))]
    public partial DateOnly? Date { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText), nameof(IsDateRowVisible), nameof(RepeatText), nameof(PlateTitle), nameof(RelText), nameof(TimeHint))]
    public partial RepeatKind Repeat { get; set; } = RepeatKind.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatText), nameof(PlateTitle), nameof(RelText), nameof(TimeHint))]
    public partial WeekDays Days { get; set; } = WeekDays.None;

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoundText))]
    public partial bool SoundEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoundText))]
    public partial string SoundId { get; set; } = Sounds.Default;

    [ObservableProperty]
    public partial bool VibrationEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorText { get; set; } = "";

    public bool HasError => !string.IsNullOrEmpty(this.ErrorText);

    /// <summary>Есть правки после загрузки: уход со страницы потребует подтверждения.</summary>
    public bool IsDirty { get; private set; }

    public string TimeText => this.Time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public string DateText => this.Date is not { } date
        ? AppResources.Nearest
        : date.ToString(this.Repeat == RepeatKind.Yearly ? "d MMMM" : "dd.MM.yyyy", CultureInfo.CurrentUICulture);

    /// <summary>Дата нужна разовому и ежегодному; ежедневному и по дням недели — только время.</summary>
    public bool IsDateRowVisible => this.Repeat is RepeatKind.None or RepeatKind.Yearly;

    public string RepeatText
    {
        get
        {
            if (this.Repeat != RepeatKind.Weekly || this.Days == WeekDays.None)
            {
                // Weekly без дней сохраняется как «Каждый день».
                return this.Repeat switch
                {
                    RepeatKind.None => AppResources.Repeat_None,
                    RepeatKind.Yearly => AppResources.Repeat_Yearly,
                    _ => AppResources.Repeat_Daily,
                };
            }

            // Дни с понедельника; AbbreviatedDayNames начинается с воскресенья.
            var names = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
            return string.Join(", ", Enumerable.Range(1, 7)
                .Select(i => (DayOfWeek)(i % 7))
                .Where(day => this.Days.HasFlag((WeekDays)(1 << (int)day)))
                .Select(day => names[(int)day]));
        }
    }

    public string SoundText => this.SoundEnabled
        ? AppResources.ResourceManager.GetString($"Sound_{this.SoundId}", AppResources.Culture) ?? this.SoundId
        : AppResources.NoSound;

    public string PlateTitle
    {
        get
        {
            var target = this.Target(this.Time);
            return this.Repeat == RepeatKind.None && this.Date is null
                ? string.Format(
                    target.Date == DateTime.Today ? AppResources.PlateToday : AppResources.PlateTomorrow,
                    target.ToString("ddd, d MMMM", CultureInfo.CurrentUICulture))
                : string.Format(AppResources.PlateDate, target.ToString("dddd, d MMMM", CultureInfo.CurrentUICulture));
        }
    }

    public string RelText => RelativeText(this.Target(this.Time));

    /// <summary>Подсказка шторки времени: сколько останется до выбранного на барабанах времени.</summary>
    public string TimeHint => RelativeText(this.Target(this.TimeSheet.Value));

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
        this.Time = new TimeOnly(this.existing.At.Hour, this.existing.At.Minute);
        this.Repeat = this.existing.Repeat;
        this.Date = this.existing.Repeat is RepeatKind.None or RepeatKind.Yearly ? DateOnly.FromDateTime(this.existing.At) : null;
        this.Days = this.existing.Days;
        this.Label = this.existing.Label;
        this.SoundEnabled = this.existing.SoundEnabled;
        this.SoundId = this.existing.SoundId;
        this.VibrationEnabled = this.existing.VibrationEnabled;

        if (query.ContainsKey("expired"))
        {
            this.ErrorText = AppResources.PickNewDate;
        }

        // Загрузка — не правка: последней, после всех присваиваний.
        this.IsDirty = false;
    }

    /// <summary>Пересчёт плашки и «через сколько» — по таймеру страницы.</summary>
    public void RefreshClock()
    {
        this.OnPropertyChanged(nameof(this.PlateTitle));
        this.OnPropertyChanged(nameof(this.RelText));
        this.OnPropertyChanged(nameof(this.TimeHint));
    }

    public void StopPreview() => this.soundPlayer.Stop();

    [RelayCommand]
    private void OpenTime()
    {
        this.TimeSheet.Load(this.Time);
        this.IsTimeSheetOpen = true;
        MainThread.BeginInvokeOnMainThread(this.TimeSheet.RequestScroll);
    }

    [RelayCommand]
    private void OpenDate()
    {
        this.CalendarSheet.Load(this.Date, DateOnly.FromDateTime(DateTime.Now), allowPast: this.Repeat == RepeatKind.Yearly);
        this.IsDateSheetOpen = true;
    }

    [RelayCommand]
    private void OpenRepeat()
    {
        this.RepeatSheet.Load(this.Repeat, this.Days);
        this.IsRepeatSheetOpen = true;
    }

    [RelayCommand]
    private void OpenSound()
    {
        this.SoundSheet.Load(this.SoundEnabled, this.SoundId);
        this.IsSoundSheetOpen = true;
    }

    /// <summary>Затемнение или системная «Назад» при открытой шторке: «Отмена» открытой шторки.</summary>
    [RelayCommand]
    private void CloseSheet()
    {
        if (this.IsTimeSheetOpen)
        {
            this.TimeSheet.CancelCommand.Execute(null);
        }
        else if (this.IsDateSheetOpen)
        {
            this.CalendarSheet.CancelCommand.Execute(null);
        }
        else if (this.IsRepeatSheetOpen)
        {
            this.RepeatSheet.CancelCommand.Execute(null);
        }
        else if (this.IsSoundSheetOpen)
        {
            this.SoundSheet.CancelCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var alarm = (this.existing ?? new Alarm()) with
        {
            At = this.Target(this.Time),
            Label = this.Label.Trim(),
            SoundEnabled = this.SoundEnabled,
            SoundId = this.SoundId,
            VibrationEnabled = this.VibrationEnabled,
            IsEnabled = true,
            Repeat = this.Repeat,
            Days = this.Repeat == RepeatKind.Weekly ? this.Days : WeekDays.None,
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
    private async Task BackAsync()
    {
        if (this.IsDirty
            && !await Shell.Current.DisplayAlertAsync(AppResources.DiscardTitle, "", AppResources.DiscardYes, AppResources.Cancel))
        {
            return;
        }

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

    // Четыре вызывающих (PlateTitle, RelText, TimeHint, сохранение). Повтор — ближайшее по правилу;
    // разовый с датой — ровно эта дата (может быть в прошлом: сохранение откажет); разовый без даты — «Ближайшее».
    private DateTime Target(TimeOnly time) => this.Repeat != RepeatKind.None
        ? new Alarm { At = (this.Date ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(time), Repeat = this.Repeat, Days = this.Days }.NextAfter(DateTime.Now)
        : this.Date is { } date
            ? date.ToDateTime(time)
            : AlarmSchedule.ResolveNearest(time, DateTime.Now);

    // Два вызывающих (RelText, TimeHint).
    private static string RelativeText(DateTime target) => AlarmSchedule.Until(target, DateTime.Now) switch
    {
        (var days, var hours, _) when days > 0 => string.Format(AppResources.RelDays, days, hours),
        (_, var hours, var minutes) when hours > 0 => string.Format(AppResources.RelHours, hours, minutes),
        (_, _, var minutes) => string.Format(AppResources.RelMinutes, minutes),
        null => AppResources.TimePassed,
    };

    partial void OnIsTimeSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsDateSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsRepeatSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsSoundSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnTimeChanged(TimeOnly value) => this.MarkDirty();

    partial void OnDateChanged(DateOnly? value) => this.MarkDirty();

    partial void OnRepeatChanged(RepeatKind value) => this.MarkDirty();

    partial void OnDaysChanged(WeekDays value) => this.MarkDirty();

    partial void OnLabelChanged(string value) => this.MarkDirty();

    partial void OnSoundEnabledChanged(bool value) => this.MarkDirty();

    partial void OnSoundIdChanged(string value) => this.MarkDirty();

    partial void OnVibrationEnabledChanged(bool value) => this.MarkDirty();

    // Восемь вызывающих (по одному на каждое сохраняемое значение).
    private void MarkDirty()
    {
        this.IsDirty = true;
        this.ErrorText = "";
    }
}
