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

    public AlarmItem(Alarm alarm, AlarmListViewModel owner)
    {
        this.owner = owner;
        this.Alarm = alarm;
        this.TimeText = alarm.At.ToString("HH:mm");
        var label = string.IsNullOrWhiteSpace(alarm.Label) ? AppResources.DefaultLabel : alarm.Label;
        this.Details = $"{alarm.At.ToString("ddd, d MMM", CultureInfo.CurrentUICulture)} · {label}";
        this.isEnabled = alarm.IsEnabled;
    }

    public Alarm Alarm { get; }

    public string TimeText { get; }

    public string Details { get; }

    // Поле, а не partial-свойство: начальное значение задаётся без вызова OnIsEnabledChanged.
    [ObservableProperty]
    private bool isEnabled;

    partial void OnIsEnabledChanged(bool value) => this.owner.OnItemToggled(this, value);
}

public sealed partial class AlarmListViewModel : ObservableObject
{
    private const string FirstRunKey = "first_run_done";

    private readonly AlarmService alarmService;
    private readonly IReliabilityChecks reliability;

    public AlarmListViewModel(AlarmService alarmService, IReliabilityChecks reliability)
    {
        this.alarmService = alarmService;
        this.reliability = reliability;
    }

    public ObservableCollection<AlarmItem> Alarms { get; } = [];

    [ObservableProperty]
    public partial string NextAlarmText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsWarningVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Вызывается из AlarmListPage.OnAppearing.</summary>
    public async Task AppearingAsync()
    {
        await RingLauncher.ShowPendingAsync();
        await SettingsLauncher.ShowPendingAsync();

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
        this.IsWarningVisible = this.alarmService.SchedulingError is not null || checks.Any(c => !c.Ok);
    }

    public void Load()
    {
        var alarms = this.alarmService.GetAll();
        this.Alarms.Clear();
        foreach (var alarm in alarms)
        {
            this.Alarms.Add(new AlarmItem(alarm, this));
        }

        this.IsEmpty = this.Alarms.Count == 0;

        var next = alarms.Where(a => a.IsEnabled).Select(a => (DateTime?)a.At).FirstOrDefault();
        if (next is null)
        {
            this.NextAlarmText = AppResources.NoActiveAlarms;
            return;
        }

        var left = next.Value - DateTime.Now;
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
    private Task OpenSettingsAsync() => Shell.Current.GoToAsync("settings");
}
