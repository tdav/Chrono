using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Строка списка: готовый к показу текст и переключатель.</summary>
public sealed partial class AlarmItem : ObservableObject
{
    private readonly AlarmListViewModel owner;

    public AlarmItem(Alarm alarm, AlarmListViewModel owner, bool showSeparator)
    {
        this.owner = owner;
        this.Alarm = alarm;
        this.ShowSeparator = showSeparator;
        this.TimeText = alarm.At.ToString("HH:mm");
        var label = string.IsNullOrWhiteSpace(alarm.Label) ? AppResources.DefaultLabel : alarm.Label;
        var culture = CultureInfo.CurrentUICulture;
        var format = culture.DateTimeFormat;
        var when = alarm.Repeat switch
        {
            RepeatKind.Daily => AppResources.Repeat_Daily,
            // Дни — с первого дня недели культуры; все семь или ни одного — «каждый день».
            RepeatKind.Weekly when alarm.Days is not WeekDays.None and not (WeekDays)0b111_1111 => string.Join(", ", Enumerable.Range(0, 7)
                .Select(i => ((int)format.FirstDayOfWeek + i) % 7)
                .Where(d => alarm.Days.HasFlag((WeekDays)(1 << d)))
                // В ru сокращения приходят строчными («пн») — первая буква заглавная.
                .Select(d => culture.TextInfo.ToUpper(format.AbbreviatedDayNames[d][0]) + format.AbbreviatedDayNames[d][1..])),
            RepeatKind.Weekly => AppResources.Repeat_Daily,
            RepeatKind.Monthly => string.Format(culture, AppResources.MonthlyOnDay, alarm.At.Day),
            RepeatKind.Yearly => $"{AppResources.Repeat_Yearly}, {alarm.At.ToString("d MMM", CultureInfo.CurrentUICulture)}",
            RepeatKind.OnceThenDelete => $"{alarm.At.ToString("ddd, d MMM", CultureInfo.CurrentUICulture)} · {AppResources.Repeat_OnceThenDelete}",
            _ => alarm.At.ToString("ddd, d MMM", CultureInfo.CurrentUICulture),
        };
        // Подпись строки — «Метка · повтор» (DESIGN.md §4.3).
        this.Details = $"{label} · {when}";
        this.isEnabled = alarm.IsEnabled;
    }

    public Alarm Alarm { get; }

    public string TimeText { get; }

    public string Details { get; }

    /// <summary>Разделитель под строкой: у последней строки секции его нет.</summary>
    public bool ShowSeparator { get; }

    // Поле, а не partial-свойство: начальное значение задаётся без вызова OnIsEnabledChanged.
    [ObservableProperty]
    private bool isEnabled;

    partial void OnIsEnabledChanged(bool value) => this.owner.OnItemToggled(this, value);
}

public sealed partial class AlarmListViewModel : ObservableObject
{
    private const string FirstRunKey = "first_run_done";

    // Проблема, на которой человек нажал ✕: подсказка молчит, пока первая проблема не станет другой.
    private const string DismissedTipKey = "reliability_tip_dismissed";

    private readonly AlarmService alarmService;
    private readonly IReliabilityChecks reliability;
    private string? tipProblem;

    public AlarmListViewModel(AlarmService alarmService, IReliabilityChecks reliability)
    {
        this.alarmService = alarmService;
        this.reliability = reliability;
    }

    public ObservableCollection<AlarmItem> Alarms { get; } = [];

    [ObservableProperty]
    public partial string NextAlarmText { get; set; } = "";

    /// <summary>Подсказка о надёжности видна, пока есть проблема, которую не скрыли.</summary>
    [ObservableProperty]
    public partial bool IsWarningVisible { get; set; }

    /// <summary>Текст подсказки: первая проблема из «Надёжности».</summary>
    [ObservableProperty]
    public partial string WarningText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarms))]
    public partial bool IsEmpty { get; set; }

    public bool HasAlarms => !this.IsEmpty;

    /// <summary>Режим «Изменить»: у строк красный «−», а кнопка в шапке называется «Готово».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditText))]
    public partial bool IsEditing { get; set; }

    public string EditText => this.IsEditing ? AppResources.SheetDone : AppResources.ListEdit;

    /// <summary>Вызывается из AlarmListPage.OnAppearing.</summary>
    public async Task AppearingAsync()
    {
        await RingLauncher.ShowPendingAsync();
        await SettingsLauncher.ShowPendingAsync();
        await ScheduleLauncher.ShowPendingAsync();

        if (!Preferences.Default.Get(FirstRunKey, false))
        {
            Preferences.Default.Set(FirstRunKey, true);
            await this.reliability.RequestPermissionsAsync();
            await Shell.Current.GoToAsync("reliability");
            return;
        }

        this.Load();

        if (this.alarmService.RecoveredFromCorruptFile)
        {
            await Shell.Current.DisplayAlertAsync(AppResources.AppName, AppResources.CorruptFile, AppResources.Ok);
        }

        var checks = await this.reliability.GetStatusAsync();

        // Первая проблема: сбой планирования важнее проверок, а они идут в порядке экрана «Надёжность».
        this.tipProblem = this.alarmService.SchedulingError is not null ? "Scheduling" : checks.FirstOrDefault(c => !c.Ok)?.Item.ToString();
        if (this.tipProblem is null)
        {
            // Проблем нет: забываем «скрыто», чтобы вернувшаяся проблема снова показала подсказку.
            Preferences.Default.Remove(DismissedTipKey);
            this.IsWarningVisible = false;
            return;
        }

        this.WarningText = AppResources.ResourceManager.GetString($"Tip_{this.tipProblem}", AppResources.Culture) ?? this.tipProblem;
        this.IsWarningVisible = Preferences.Default.Get(DismissedTipKey, "") != this.tipProblem;
    }

    public void Load()
    {
        // Вкладка «Будильники» — только записи со звонком; «Уведомление» живёт в «Распорядке».
        var alarms = this.alarmService.GetAll().Where(a => a.Alert == AlertKind.Ring).ToList();
        this.Alarms.Clear();
        for (var i = 0; i < alarms.Count; i++)
        {
            this.Alarms.Add(new AlarmItem(alarms[i], this, i < alarms.Count - 1));
        }

        this.IsEmpty = this.Alarms.Count == 0;

        // Все будильники удалены в режиме «Изменить» — править больше нечего.
        if (this.IsEmpty)
        {
            this.IsEditing = false;
        }

        // Ближайший момент оповещения: напоминание звонит раньше At. Сработавшие, но не истёкшие записи
        // («Один раз и удалить» с At <= now до конца звонка) не считаются: берутся только моменты позже now.
        // Закончившаяся серия сохраняет IsEnabled и At после EndsOn, но уже не прозвонит — её пропускаем.
        var now = DateTime.Now;
        var next = alarms.Where(a => a.IsEnabled && !a.Ended(a.At)).SelectMany(a => a.AlertTimes(a.At)).Where(t => t > now).Select(t => (DateTime?)t).Min();
        if (next is null)
        {
            this.NextAlarmText = AppResources.NoActiveAlarms;
            return;
        }

        var left = next.Value - now;
        this.NextAlarmText = left.TotalHours >= 1
            ? string.Format(AppResources.NextInHours, (int)left.TotalHours, left.Minutes)
            : string.Format(AppResources.NextInMinutes, Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));
    }

    public void OnItemToggled(AlarmItem item, bool enabled)
    {
        // Переключатель ещё в процессе анимации — перестраиваем список после неё.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!this.alarmService.Toggle(item.Alarm.Id, enabled))
            {
                // Время прошло: включать нечего — предлагаем выбрать новую дату.
                await Shell.Current.GoToAsync($"edit?id={item.Alarm.Id}&expired=true");
                return;
            }

            this.Load();
        });
    }

    [RelayCommand]
    private Task AddAsync() => Shell.Current.GoToAsync("edit");

    [RelayCommand]
    private Task EditAsync(AlarmItem item) => Shell.Current.GoToAsync($"edit?id={item.Alarm.Id}");

    [RelayCommand]
    private void Delete(AlarmItem item)
    {
        this.alarmService.Delete(item.Alarm.Id);
        this.Load();
    }

    [RelayCommand]
    private Task OpenReliabilityAsync() => Shell.Current.GoToAsync("reliability");

    [RelayCommand]
    private void ToggleEdit() => this.IsEditing = !this.IsEditing;

    [RelayCommand]
    private void DismissWarning()
    {
        // Запись очищает AppearingAsync, когда проблем не осталось.
        Preferences.Default.Set(DismissedTipKey, this.tipProblem ?? "");
        this.IsWarningVisible = false;
    }
}
