using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Число на барабане: выбранное рисуется крупно и тёмным.</summary>
public sealed partial class WheelItem : ObservableObject
{
    public WheelItem(int value)
    {
        this.Value = value;
        this.Label = value.ToString("00", CultureInfo.InvariantCulture);
    }

    public int Value { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Черновик шторки времени: барабаны часов и минут, быстрые чипы. Применяется только по «Окей».</summary>
public sealed partial class TimeSheetModel : ObservableObject
{
    public TimeSheetModel()
    {
        // Начальные 00:00: присваивание значения по умолчанию не вызывает OnHourChanged — отмечаем явно.
        this.Hours[0].IsSelected = true;
        this.Minutes[0].IsSelected = true;
    }

    public IReadOnlyList<WheelItem> Hours { get; } = Enumerable.Range(0, 24).Select(i => new WheelItem(i)).ToArray();

    public IReadOnlyList<WheelItem> Minutes { get; } = Enumerable.Range(0, 60).Select(i => new WheelItem(i)).ToArray();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value))]
    public partial int Hour { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value))]
    public partial int Minute { get; set; }

    public TimeOnly Value => new(this.Hour, this.Minute);

    /// <summary>Просит представление прокрутить барабаны к Hour/Minute.</summary>
    public event EventHandler? ScrollRequested;

    /// <summary>true — «Окей», false — «Отмена».</summary>
    public event EventHandler<bool>? Done;

    public void Load(TimeOnly time)
    {
        this.Hour = time.Hour;
        this.Minute = time.Minute;
        this.RequestScroll();
    }

    public void RequestScroll() => this.ScrollRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Quick(string hhmm) => this.Load(TimeOnly.ParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture));

    [RelayCommand]
    private void Now() => this.Load(TimeOnly.FromDateTime(DateTime.Now));

    [RelayCommand]
    private void PickHour(WheelItem item)
    {
        this.Hour = item.Value;
        this.RequestScroll();
    }

    [RelayCommand]
    private void PickMinute(WheelItem item)
    {
        this.Minute = item.Value;
        this.RequestScroll();
    }

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnHourChanged(int value)
    {
        foreach (var item in this.Hours)
        {
            item.IsSelected = item.Value == value;
        }
    }

    partial void OnMinuteChanged(int value)
    {
        foreach (var item in this.Minutes)
        {
            item.IsSelected = item.Value == value;
        }
    }
}
