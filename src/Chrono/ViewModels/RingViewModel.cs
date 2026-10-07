using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class RingViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly IAlarmScheduler scheduler;
    private Guid alarmId;

    public RingViewModel(AlarmService alarmService, IAlarmScheduler scheduler)
    {
        this.alarmService = alarmService;
        this.scheduler = scheduler;
    }

    [ObservableProperty]
    public partial string TimeText { get; set; } = "";

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial string DateText { get; set; } = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            this.Load(id);
        }
    }

    public void Load(Guid id)
    {
        this.alarmId = id;
        var alarm = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
        var at = alarm?.At ?? DateTime.Now;
        this.TimeText = at.ToString("HH:mm");
        this.DateText = at.ToString("dddd, d MMMM", CultureInfo.CurrentUICulture);
        this.Label = string.IsNullOrWhiteSpace(alarm?.Label) ? AppResources.DefaultLabel : alarm.Label;
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        this.scheduler.StopRinging(this.alarmId);
        await Shell.Current.GoToAsync("..");
    }
}
