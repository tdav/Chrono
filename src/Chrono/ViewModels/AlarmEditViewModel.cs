using System.Globalization;
using Chrono.Design;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>
/// Создание и редактирование (DESIGN.md §4.1). Сохранение всегда включает будильник (как в системных «Часах»)
/// и требует время в будущем. Время, дата и окончание выбираются в форме (барабан и календари), повтор, оповещение,
/// длительность и иконка — шагами шторки, звук — меню. Выбор в них применяется к форме сразу, а в запись уходит по ✓.
/// </summary>
public sealed partial class AlarmEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly ISoundPlayer soundPlayer;
    private Alarm? existing;

    // Идёт загрузка значений формы в модели календарей и шагов: их уведомления в форму не возвращаются
    // (загрузка — не правка, а длительность при загрузке ещё и обрезается до 720).
    private bool syncing;

    public AlarmEditViewModel(AlarmService alarmService, ISoundPlayer soundPlayer)
    {
        this.alarmService = alarmService;
        this.soundPlayer = soundPlayer;
        this.SoundSheet = new SoundSheetModel(soundPlayer);
        // Порядок задан явно, как в DESIGN.md §3.6: «Короткий сигнал» перед «Звонить как будильник» (enum хранится числом, и Beep добавлен в его конец).
        this.AlertSheet = new OptionsSheetModel(new[] { AlertKind.None, AlertKind.Notify, AlertKind.Beep, AlertKind.Ring }.Select(kind =>
            new ChoiceOption(kind.ToString(), AppResources.ResourceManager.GetString($"Alert_{kind}", AppResources.Culture) ?? kind.ToString())));
        this.IconSheet = new OptionsSheetModel(ItemIcons.Keys.Select(key =>
            new ChoiceOption(key, AppResources.ResourceManager.GetString($"Icon_{key}", AppResources.Culture) ?? key)));

        // Сноска под барабаном считает момент срабатывания по правилу записи (дата, повтор), а не по «Ближайшему».
        this.TimeSheet.Target = this.Target;
        var start = DateTime.Now.AddHours(1);
        this.TimeSheet.Load(new TimeOnly(start.Hour, start.Minute));
        this.TimeSheet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TimeSheetModel.Hour) or nameof(TimeSheetModel.Minute))
            {
                this.MarkDirty();
            }
        };

        // Календари, шаги и меню звука применяются к форме сразу (как списки выбора iOS): «назад» из шага просто возвращает на форму.
        this.CalendarSheet.PropertyChanged += (_, e) =>
        {
            if (!this.syncing && e.PropertyName == nameof(CalendarSheetModel.Selected))
            {
                // Ежегодному и ежемесячному нужна дата (день и месяц / число): «Ближайшее» для них не имеет смысла.
                this.Date = this.CalendarSheet.Selected ?? (this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly ? DateOnly.FromDateTime(DateTime.Now) : null);
            }
        };

        // «Очистить» у окончания скрыта: пустого выбора не бывает, сбросить окончание нельзя, оно обязательно.
        this.EndsSheet.PropertyChanged += (_, e) =>
        {
            if (!this.syncing && e.PropertyName == nameof(CalendarSheetModel.Selected) && this.EndsSheet.Selected is { } ends)
            {
                this.EndsOn = ends;
            }
        };

        this.RepeatSheet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RepeatSheetModel.Repeat))
            {
                this.ApplyRepeat();
            }
        };
        foreach (var day in this.RepeatSheet.Days)
        {
            day.PropertyChanged += (_, _) => this.ApplyRepeat();
        }

        this.AlertSheet.PropertyChanged += (_, e) =>
        {
            if (!this.syncing && e.PropertyName == nameof(OptionsSheetModel.SelectedId))
            {
                this.Alert = Enum.Parse<AlertKind>(this.AlertSheet.SelectedId);
            }
        };

        this.IconSheet.PropertyChanged += (_, e) =>
        {
            if (!this.syncing && e.PropertyName == nameof(OptionsSheetModel.SelectedId))
            {
                this.Icon = this.IconSheet.SelectedId;
            }
        };

        this.DurationSheet.PropertyChanged += (_, e) =>
        {
            if (!this.syncing && e.PropertyName == nameof(DurationSheetModel.Minutes))
            {
                this.DurationMinutes = this.DurationSheet.Minutes;
            }
        };

        // «Без звука» (Id "") выключает звук, последняя выбранная мелодия остаётся в SoundId (DESIGN.md §3.5).
        this.SoundSheet.PropertyChanged += (_, e) =>
        {
            if (!this.syncing && e.PropertyName == nameof(SoundSheetModel.SelectedId))
            {
                this.SoundEnabled = this.SoundSheet.SelectedId.Length > 0;
                if (this.SoundEnabled)
                {
                    this.SoundId = this.SoundSheet.SelectedId;
                }
            }
        };

        this.SyncSheets();

        // Начальные значения не считаются правками.
        this.IsDirty = false;
    }

    /// <summary>Барабан времени: часы и минуты формы, сноска «Сработает … — через …».</summary>
    public TimeSheetModel TimeSheet { get; } = new();

    public CalendarSheetModel CalendarSheet { get; } = new();

    /// <summary>Календарь окончания: тот же, но пока дата не выбрана, значение чипа — «Не выбрано».</summary>
    public CalendarSheetModel EndsSheet { get; } = new() { EmptyChipText = AppResources.EndsNotSet };

    public RepeatSheetModel RepeatSheet { get; } = new();

    public SoundSheetModel SoundSheet { get; }

    public OptionsSheetModel AlertSheet { get; }

    public OptionsSheetModel IconSheet { get; }

    public DurationSheetModel DurationSheet { get; } = new();

    /// <summary>Раскрыт календарь «Даты»: два календаря сразу не раскрываются.</summary>
    [ObservableProperty]
    public partial bool IsDateExpanded { get; set; }

    /// <summary>Раскрыт календарь «Окончания».</summary>
    [ObservableProperty]
    public partial bool IsEndsExpanded { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = AppResources.AlarmSheetNew;

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    /// <summary>null — «Ближайшее» (сегодня или завтра по времени).</summary>
    [ObservableProperty]
    public partial DateOnly? Date { get; set; }

    /// <summary>Последний день повторяющейся записи включительно; у повторяющейся обязателен (иначе сохранение недоступно), у разовой не хранится.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEndsMissing))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DateOnly? EndsOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDateRowVisible), nameof(IsEndsRowVisible), nameof(IsEndsMissing), nameof(RepeatText), nameof(IsDaysMissing), nameof(CanConfirm))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial RepeatKind Repeat { get; set; } = RepeatKind.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatText), nameof(IsDaysMissing), nameof(CanConfirm))]
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
    [NotifyPropertyChangedFor(nameof(HasError), nameof(CanConfirm))]
    public partial string ErrorText { get; set; } = "";

    public bool HasError => !string.IsNullOrEmpty(this.ErrorText);

    /// <summary>«По дням недели» без выбранных дней — ошибка формы (DESIGN.md §3.4).</summary>
    public bool IsDaysMissing => this.Repeat == RepeatKind.Weekly && this.Days == WeekDays.None;

    /// <summary>false — в форме ошибка (неудачное сохранение или пустой набор дней): ✓ неактивна, пока её не исправят.</summary>
    public bool CanConfirm => !this.HasError && !this.IsDaysMissing;

    /// <summary>Есть правки после загрузки: закрытие шторки без ✓ спросит «Не сохранять / Сохранить / Отменить».</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Уход со страницы уже начат (сохранение, закрытие или удаление): повторные нажатия и «Назад» второго перехода не делают.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Дата нужна разовому («Однократно», «Один раз и удалить»), ежегодному и ежемесячному; ежедневному и по дням недели — только время.</summary>
    public bool IsDateRowVisible => this.IsOneShot || this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly;

    /// <summary>Окончание есть только у повторяющейся записи.</summary>
    public bool IsEndsRowVisible => !this.IsOneShot;

    /// <summary>Дата окончания обязательна, но ещё не выбрана: под секцией подсказка, ✓ неактивна.</summary>
    public bool IsEndsMissing => this.IsEndsRowVisible && this.EndsOn is null;

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
                // «По дням недели» без дней — ошибка формы (IsDaysMissing): строка называет выбранный вид, а не «Каждый день».
                return this.Repeat switch
                {
                    RepeatKind.None => AppResources.Repeat_None,
                    RepeatKind.OnceThenDelete => AppResources.Repeat_OnceThenDelete,
                    RepeatKind.Weekly => AppResources.Repeat_Weekly,
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            this.existing = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
            if (this.existing is null)
            {
                return;
            }

            this.Title = AppResources.AlarmSheetEdit;
            this.IsExisting = true;
            this.TimeSheet.Load(new TimeOnly(this.existing.At.Hour, this.existing.At.Minute));
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

            // Начало и длительность из циферблата: time=HH:mm, duration=N (минуты, 1..720 — как в шаге длительности).
            if (query.TryGetValue("time", out var timeValue)
                && TimeOnly.TryParseExact(timeValue?.ToString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
            {
                this.TimeSheet.Load(parsedTime);
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
        this.SyncSheets();
        if (this.existing is not null && query.ContainsKey("expired"))
        {
            this.ErrorText = AppResources.PickNewDate;
        }

        this.IsDirty = false;
    }

    /// <summary>Пересчёт сноски под барабаном («через сколько») — по таймеру страницы.</summary>
    public void RefreshClock() => this.TimeSheet.RefreshFootnote();

    public void StopPreview() => this.soundPlayer.Stop();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (this.IsLeaving)
        {
            return;
        }

        var at = this.Target(this.TimeSheet.Value);
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

        if (this.TryLeave())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    /// <summary>✕, затемнение, свайп вниз и «Назад»: правки, если они были, шторка уже спросила («Не сохранять» / «Сохранить» / «Отменить»).</summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        if (this.TryLeave())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        // Страница уже уезжает (✓ или ✕): вопрос над ней не показываем.
        if (this.IsLeaving)
        {
            return;
        }

        // Общий модальный вопрос: на Android «Удалить будильник» крайняя справа, «Отменить» слева от неё; на iOS — action sheet.
        var choice = await ChoiceDialog.ShowAsync(Shell.Current, AppResources.DeleteAlarmQuestion, AppResources.SheetCancel, AppResources.DeleteAlarm);
        if (choice != AppResources.DeleteAlarm || !this.TryLeave())
        {
            return;
        }

        if (this.existing is not null)
        {
            this.alarmService.Delete(this.existing.Id);
        }

        await Shell.Current.GoToAsync("..");
    }

    // Три вызывающих (сохранение, закрытие, удаление): уход со страницы начинается один раз. Правок после него нет —
    // повторный ✕ или свайп не спросит про них заново; прослушивание мелодии на уходе останавливается.
    private bool TryLeave()
    {
        if (this.IsLeaving)
        {
            return false;
        }

        this.IsLeaving = true;
        this.IsDirty = false;
        this.soundPlayer.Stop();
        return true;
    }

    // Два вызывающих (конструктор и загрузка записи): календари и шаги показывают значения формы.
    private void SyncSheets()
    {
        this.syncing = true;
        this.LoadDateCalendar();
        this.EndsSheet.Load(this.EndsOn, DateOnly.FromDateTime(DateTime.Now), allowPast: false);

        // Окончание обязательно: «Очистить» (вернуть «не выбрано») недоступна.
        this.EndsSheet.IsClearVisible = false;
        this.RepeatSheet.Load(this.Repeat, this.Days);
        this.AlertSheet.Load(this.Alert.ToString());
        this.IconSheet.Load(this.Icon);
        this.DurationSheet.Load(this.DurationMinutes);
        this.SoundSheet.Load(this.SoundEnabled, this.SoundId);
        this.syncing = false;
    }

    // Два вызывающих (SyncSheets и смена повтора): у ежегодного и ежемесячного доступны и прошедшие дни, а «Ближайшее» скрыто.
    private void LoadDateCalendar()
    {
        this.CalendarSheet.IsYearly = this.Repeat == RepeatKind.Yearly;
        this.CalendarSheet.Load(this.Date, DateOnly.FromDateTime(DateTime.Now), allowPast: this.Repeat is RepeatKind.Yearly or RepeatKind.Monthly);
    }

    // Два вызывающих (смена вида повтора и смена дня в шаге): итог выбора переносится в форму сразу.
    private void ApplyRepeat()
    {
        if (this.syncing)
        {
            return;
        }

        var repeat = this.RepeatSheet.Repeat;

        // Ежегодному и ежемесячному нужна дата: «Ближайшее» для них не имеет смысла.
        if (repeat is RepeatKind.Yearly or RepeatKind.Monthly && this.Date is null)
        {
            this.Date = DateOnly.FromDateTime(DateTime.Now);
        }

        this.Repeat = repeat;
        this.Days = repeat == RepeatKind.Weekly ? this.RepeatSheet.SelectedDays : WeekDays.None;
    }

    // Два вызывающих (сноска под барабаном и сохранение). Повтор — ближайшее по правилу не раньше StartsOn;
    // разовый с датой — ровно эта дата (может быть в прошлом: сохранение откажет); разовый без даты — «Ближайшее».
    private DateTime Target(TimeOnly time) => !this.IsOneShot
        ? new Alarm { At = (this.Date ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(time), Repeat = this.Repeat, Days = this.Days, StartsOn = this.Date is { } start && this.IsDateRowVisible ? start : this.StartsOn }.NextAfter(DateTime.Now)
        : this.Date is { } date
            ? date.ToDateTime(time)
            : AlarmSchedule.ResolveNearest(time, DateTime.Now);

    partial void OnIsDateExpandedChanged(bool value)
    {
        if (value)
        {
            this.IsEndsExpanded = false;
        }
    }

    partial void OnIsEndsExpandedChanged(bool value)
    {
        if (value)
        {
            this.IsDateExpanded = false;
        }
    }

    partial void OnDateChanged(DateOnly? value)
    {
        this.MarkDirty();
        this.TimeSheet.RefreshFootnote();
    }

    partial void OnEndsOnChanged(DateOnly? value) => this.MarkDirty();

    partial void OnRepeatChanged(RepeatKind value)
    {
        this.MarkDirty();
        this.TimeSheet.RefreshFootnote();
        this.LoadDateCalendar();
    }

    partial void OnDaysChanged(WeekDays value)
    {
        this.MarkDirty();
        this.TimeSheet.RefreshFootnote();
    }

    partial void OnLabelChanged(string value) => this.MarkDirty();

    partial void OnSoundEnabledChanged(bool value) => this.MarkDirty();

    partial void OnSoundIdChanged(string value) => this.MarkDirty();

    partial void OnVibrationEnabledChanged(bool value) => this.MarkDirty();

    partial void OnIconChanged(string value) => this.MarkDirty();

    partial void OnDurationMinutesChanged(int value) => this.MarkDirty();

    partial void OnAlertChanged(AlertKind value) => this.MarkDirty();

    // Двенадцать вызывающих (по одному на каждое сохраняемое значение).
    private void MarkDirty()
    {
        // Страница уже уезжает: касание барабана в эти 250 мс не должно вызвать вопрос о правках поверх неё.
        if (this.IsLeaving)
        {
            return;
        }

        this.IsDirty = true;
        this.ErrorText = "";
    }
}
