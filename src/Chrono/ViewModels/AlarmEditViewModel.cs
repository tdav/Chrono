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
        this.AlertSheet = new OptionsSheetModel(Enum.GetValues<AlertKind>().Select(kind =>
            new ChoiceOption(kind.ToString(), AppResources.ResourceManager.GetString($"Alert_{kind}", AppResources.Culture) ?? kind.ToString())));
        this.IconSheet = new OptionsSheetModel(ItemIcons.Keys.Select(key =>
            new ChoiceOption(key, AppResources.ResourceManager.GetString($"Icon_{key}", AppResources.Culture) ?? key)));

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
                // Ежегодному и ежемесячному нужна дата (день и месяц / число): «Ближайшее» для них не имеет смысла.
                this.Date = this.CalendarSheet.Selected ?? (this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly ? DateOnly.FromDateTime(DateTime.Now) : null);
            }

            this.IsDateSheetOpen = false;
        };

        // Пустой выбор («Очистить» скрыта) оставляет прежнюю дату окончания: сбросить её нельзя, она обязательна.
        this.EndsSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.EndsOn = this.EndsSheet.Selected ?? this.EndsOn;
            }

            this.IsEndsSheetOpen = false;
        };

        this.RepeatSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.Repeat = this.RepeatSheet.Repeat;
                this.Days = this.RepeatSheet.Repeat == RepeatKind.Weekly ? this.RepeatSheet.SelectedDays : WeekDays.None;

                // Ежегодному и ежемесячному нужна дата: «Ближайшее» для них не имеет смысла.
                if (this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly && this.Date is null)
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

        this.AlertSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.Alert = Enum.Parse<AlertKind>(this.AlertSheet.SelectedId);
            }

            this.IsAlertSheetOpen = false;
        };

        this.IconSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.Icon = this.IconSheet.SelectedId;
            }

            this.IsIconSheetOpen = false;
        };

        // Минуты читаются только при «Окей»: при «Отмена» поле своего значения может быть недопустимым.
        this.DurationSheet.Done += (_, accepted) =>
        {
            if (accepted)
            {
                this.DurationMinutes = this.DurationSheet.Minutes;
            }

            this.IsDurationSheetOpen = false;
        };

        // Начальные значения не считаются правками.
        this.IsDirty = false;
    }

    public TimeSheetModel TimeSheet { get; } = new();

    public CalendarSheetModel CalendarSheet { get; } = new();

    /// <summary>Шторка даты окончания: тот же календарь, но с другой подсказкой и без «Очистить».</summary>
    public CalendarSheetModel EndsSheet { get; } = new() { EmptyHint = AppResources.EndsPickHint };

    public RepeatSheetModel RepeatSheet { get; } = new();

    public SoundSheetModel SoundSheet { get; }

    public OptionsSheetModel AlertSheet { get; }

    public OptionsSheetModel IconSheet { get; }

    public DurationSheetModel DurationSheet { get; } = new();

    [ObservableProperty]
    public partial bool IsTimeSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsDateSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsEndsSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsRepeatSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsSoundSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsAlertSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsIconSheetOpen { get; set; }

    [ObservableProperty]
    public partial bool IsDurationSheetOpen { get; set; }

    public bool IsAnySheetOpen => this.IsTimeSheetOpen || this.IsDateSheetOpen || this.IsEndsSheetOpen || this.IsRepeatSheetOpen || this.IsSoundSheetOpen
        || this.IsAlertSheetOpen || this.IsIconSheetOpen || this.IsDurationSheetOpen;

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

    /// <summary>Последний день повторяющейся записи включительно; у повторяющейся обязателен (иначе сохранение недоступно), у разовой не хранится.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndsText), nameof(IsEndsMissing))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DateOnly? EndsOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText), nameof(IsDateRowVisible), nameof(IsEndsRowVisible), nameof(IsEndsMissing), nameof(RepeatText), nameof(PlateTitle), nameof(RelText), nameof(TimeHint))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
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
    [NotifyPropertyChangedFor(nameof(IconImage))]
    public partial string Icon { get; set; } = "bell";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    public partial int DurationMinutes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlertText), nameof(IsSoundRowsVisible))]
    public partial AlertKind Alert { get; set; } = AlertKind.Ring;

    /// <summary>Первый день действия записи; задаётся только при загрузке, поэтому правкой не считается.</summary>
    [ObservableProperty]
    public partial DateOnly StartsOn { get; set; } = DateOnly.FromDateTime(DateTime.Now);

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

    /// <summary>Дата нужна разовому («Однократно», «Один раз и удалить»), ежегодному и ежемесячному; ежедневному и по дням недели — только время.</summary>
    public bool IsDateRowVisible => this.IsOneShot || this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly;

    /// <summary>Окончание есть только у повторяющейся записи.</summary>
    public bool IsEndsRowVisible => !this.IsOneShot;

    /// <summary>Дата окончания обязательна, но ещё не выбрана: строка подсвечивается.</summary>
    public bool IsEndsMissing => this.IsEndsRowVisible && this.EndsOn is null;

    public string EndsText => this.EndsOn is { } date
        ? date.ToString("dd.MM.yyyy", CultureInfo.CurrentUICulture)
        : AppResources.EndsNotSet;

    /// <summary>Повторяющейся записи без даты окончания сохраняться нельзя.</summary>
    private bool CanSave => this.IsOneShot || this.EndsOn is not null;

    /// <summary>Звук и вибрация имеют смысл только у звонящей записи.</summary>
    public bool IsSoundRowsVisible => this.Alert == AlertKind.Ring;

    public string IconImage => ItemIcons.ImageFor(this.Icon);

    public string DurationText => this.DurationMinutes == 0
        ? AppResources.DurationNone
        : string.Format(AppResources.Culture ?? CultureInfo.CurrentUICulture, AppResources.DurationMinutes, this.DurationMinutes);

    public string AlertText => AppResources.ResourceManager.GetString($"Alert_{this.Alert}", AppResources.Culture) ?? this.Alert.ToString();

    /// <summary>Разовая запись: «Однократно» или «Один раз и удалить» — то же, что <see cref="Alarm.IsOneShot"/>.</summary>
    private bool IsOneShot => this.Repeat is RepeatKind.None or RepeatKind.OnceThenDelete;

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
                    RepeatKind.OnceThenDelete => AppResources.Repeat_OnceThenDelete,
                    RepeatKind.Yearly => AppResources.Repeat_Yearly,
                    RepeatKind.Monthly => AppResources.Repeat_Monthly,
                    _ => AppResources.Repeat_Daily,
                };
            }

            // Дни с понедельника; AbbreviatedDayNames начинается с воскресенья.
            // В ru сокращения приходят строчными («пн») — первая буква заглавная.
            var culture = CultureInfo.CurrentUICulture;
            var names = culture.DateTimeFormat.AbbreviatedDayNames;
            return string.Join(", ", Enumerable.Range(1, 7)
                .Select(i => (DayOfWeek)(i % 7))
                .Where(day => this.Days.HasFlag((WeekDays)(1 << (int)day)))
                .Select(day => culture.TextInfo.ToUpper(names[(int)day][0]) + names[(int)day][1..]));
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
            return this.IsOneShot && this.Date is null
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
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            this.existing = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
            if (this.existing is null)
            {
                return;
            }

            this.Title = AppResources.EditAlarm;
            this.IsExisting = true;
            this.Time = new TimeOnly(this.existing.At.Hour, this.existing.At.Minute);
            this.Repeat = this.existing.Repeat;
            this.Date = this.existing.IsOneShot() || this.existing.Repeat is RepeatKind.Yearly or RepeatKind.Monthly ? DateOnly.FromDateTime(this.existing.At) : null;
            this.EndsOn = this.existing.EndsOn;
            this.Days = this.existing.Days;
            this.Label = this.existing.Label;
            this.SoundEnabled = this.existing.SoundEnabled;
            this.SoundId = this.existing.SoundId;
            this.VibrationEnabled = this.existing.VibrationEnabled;
            this.Icon = this.existing.Icon;
            this.DurationMinutes = this.existing.DurationMinutes;
            this.Alert = this.existing.Alert;
            this.StartsOn = this.existing.StartsOn ?? DateOnly.FromDateTime(this.existing.At);

            if (query.ContainsKey("expired"))
            {
                this.ErrorText = AppResources.PickNewDate;
            }
        }
        else
        {
            // Создание с экрана «Распорядок»: день (yyyy-MM-dd) и уровень оповещения (имя AlertKind).
            var today = DateOnly.FromDateTime(DateTime.Now);
            var day = query.TryGetValue("day", out var dayValue)
                && DateOnly.TryParseExact(dayValue?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDay)
                    ? parsedDay
                    : (DateOnly?)null;
            if (query.TryGetValue("alert", out var alertValue)
                && Enum.TryParse<AlertKind>(alertValue?.ToString(), out var alert)
                && Enum.IsDefined(alert))
            {
                this.Alert = alert;
            }

            // Начало и длительность из циферблата: time=HH:mm, duration=N (минуты, 1..720 — как в шторке длительности).
            if (query.TryGetValue("time", out var timeValue)
                && TimeOnly.TryParseExact(timeValue?.ToString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
            {
                this.Time = parsedTime;
            }

            if (query.TryGetValue("duration", out var durationValue)
                && int.TryParse(durationValue?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration)
                && parsedDuration is > 0 and <= DurationSheetModel.MaxMinutes)
            {
                this.DurationMinutes = parsedDuration;
            }

            this.StartsOn = day ?? today;

            // Для сегодняшнего дня дата не задаётся: «Ближайшее» само выберет сегодня или завтра по времени.
            this.Date = day > today ? day : null;
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
    }

    [RelayCommand]
    private void OpenDate()
    {
        this.CalendarSheet.Load(this.Date, DateOnly.FromDateTime(DateTime.Now), allowPast: this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly);
        this.IsDateSheetOpen = true;
    }

    [RelayCommand]
    private void OpenEnds()
    {
        this.EndsSheet.Load(this.EndsOn, DateOnly.FromDateTime(DateTime.Now), allowPast: false);

        // Окончание обязательно: «Очистить» (вернуть «не выбрано») недоступна.
        this.EndsSheet.IsClearVisible = false;
        this.IsEndsSheetOpen = true;
    }

    [RelayCommand]
    private void OpenRepeat()
    {
        this.RepeatSheet.Load(this.Repeat, this.Days);
        this.IsRepeatSheetOpen = true;
    }

    [RelayCommand]
    private void OpenAlert()
    {
        this.AlertSheet.Load(this.Alert.ToString());
        this.IsAlertSheetOpen = true;
    }

    [RelayCommand]
    private void OpenIcon()
    {
        this.IconSheet.Load(this.Icon);
        this.IsIconSheetOpen = true;
    }

    [RelayCommand]
    private void OpenDuration()
    {
        this.DurationSheet.Load(this.DurationMinutes);
        this.IsDurationSheetOpen = true;
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
        else if (this.IsEndsSheetOpen)
        {
            this.EndsSheet.CancelCommand.Execute(null);
        }
        else if (this.IsRepeatSheetOpen)
        {
            this.RepeatSheet.CancelCommand.Execute(null);
        }
        else if (this.IsSoundSheetOpen)
        {
            this.SoundSheet.CancelCommand.Execute(null);
        }
        else if (this.IsAlertSheetOpen)
        {
            this.AlertSheet.CancelCommand.Execute(null);
        }
        else if (this.IsIconSheetOpen)
        {
            this.IconSheet.CancelCommand.Execute(null);
        }
        else if (this.IsDurationSheetOpen)
        {
            this.DurationSheet.CancelCommand.Execute(null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var at = this.Target(this.Time);
        var alarm = (this.existing ?? new Alarm()) with
        {
            At = at,
            Label = this.Label.Trim(),
            SoundEnabled = this.SoundEnabled,
            SoundId = this.SoundId,
            VibrationEnabled = this.VibrationEnabled,
            IsEnabled = true,
            Repeat = this.Repeat,
            Days = this.Repeat == RepeatKind.Weekly ? this.Days : WeekDays.None,
            Icon = this.Icon,
            DurationMinutes = this.DurationMinutes,
            Alert = this.Alert,
            // Разовая начинается в день срабатывания (At уже равен Date + время; без даты StartsOn из ленты,
            // ушедший вперёд, скрыл бы запись в её дне). Ежегодная — с выбранной даты. У ежедневной и «по дням
            // недели» строка даты скрыта, поэтому оставшийся от прежнего выбора Date не сдвигает начало.
            StartsOn = this.IsOneShot ? DateOnly.FromDateTime(at) : this.Date is { } start && this.IsDateRowVisible ? start : this.StartsOn,
            // У разовой записи окончания нет; у повторяющейся оно обязательно (CanSave), но прежнее значение, оставшееся
            // после смены повтора на разовый, не должно утечь в запись.
            EndsOn = this.IsOneShot ? null : this.EndsOn,
        };

        // At повторяющейся — ближайшее срабатывание (Target): если оно уже после окончания, запись не прозвенит ни разу.
        // Условие покрывает и EndsOn < StartsOn (At не раньше StartsOn). Для разовой Ended всегда false.
        if (alarm.Ended(alarm.At))
        {
            this.ErrorText = AppResources.EndsBeforeStart;
            return;
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

    // Четыре вызывающих (PlateTitle, RelText, TimeHint, сохранение). Повтор — ближайшее по правилу не раньше StartsOn;
    // разовый с датой — ровно эта дата (может быть в прошлом: сохранение откажет); разовый без даты — «Ближайшее».
    private DateTime Target(TimeOnly time) => !this.IsOneShot
        ? new Alarm { At = (this.Date ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(time), Repeat = this.Repeat, Days = this.Days, StartsOn = this.Date is { } start && this.IsDateRowVisible ? start : this.StartsOn }.NextAfter(DateTime.Now)
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

    partial void OnIsEndsSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsRepeatSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsSoundSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsAlertSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsIconSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnIsDurationSheetOpenChanged(bool value) => this.OnPropertyChanged(nameof(this.IsAnySheetOpen));

    partial void OnTimeChanged(TimeOnly value) => this.MarkDirty();

    partial void OnDateChanged(DateOnly? value) => this.MarkDirty();

    partial void OnEndsOnChanged(DateOnly? value) => this.MarkDirty();

    partial void OnRepeatChanged(RepeatKind value) => this.MarkDirty();

    partial void OnDaysChanged(WeekDays value) => this.MarkDirty();

    partial void OnLabelChanged(string value) => this.MarkDirty();

    partial void OnSoundEnabledChanged(bool value) => this.MarkDirty();

    partial void OnSoundIdChanged(string value) => this.MarkDirty();

    partial void OnVibrationEnabledChanged(bool value) => this.MarkDirty();

    partial void OnIconChanged(string value) => this.MarkDirty();

    partial void OnDurationMinutesChanged(int value) => this.MarkDirty();

    partial void OnAlertChanged(AlertKind value) => this.MarkDirty();

    // Одиннадцать вызывающих (по одному на каждое сохраняемое значение).
    private void MarkDirty()
    {
        this.IsDirty = true;
        this.ErrorText = "";
    }
}
