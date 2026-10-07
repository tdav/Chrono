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
        this.Date = start.Date;
        this.Time = new TimeSpan(start.Hour, start.Minute, 0);
        // Инициализатор свойства не вызывает OnSoundIdChanged — отмечаем мелодию по умолчанию явно.
        this.OnSoundIdChanged(this.SoundId);
    }

    public ObservableCollection<SoundOption> SoundOptions { get; }

    public DateTime MinimumDate => DateTime.Today;

    [ObservableProperty]
    public partial string Title { get; set; } = AppResources.NewAlarm;

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial TimeSpan? Time { get; set; }

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
        this.Date = this.existing.At.Date;
        this.Time = this.existing.At.TimeOfDay;
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
        var at = (this.Date ?? DateTime.Today).Date + (this.Time ?? TimeSpan.Zero);
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

    partial void OnDateChanged(DateTime? value) => this.ErrorText = "";

    partial void OnTimeChanged(TimeSpan? value) => this.ErrorText = "";

    partial void OnSoundIdChanged(string value)
    {
        foreach (var option in this.SoundOptions)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
