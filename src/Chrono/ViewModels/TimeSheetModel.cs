using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Быстрый вариант времени под барабаном (кнопка-переключатель): «вкл», пока на барабане именно оно.</summary>
public sealed partial class QuickTime : ObservableObject
{
    public QuickTime(int hour, int minute)
    {
        this.Time = new TimeOnly(hour, minute);
        this.Label = this.Time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public TimeOnly Time { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

/// <summary>Черновик шторки времени: барабаны часов и минут, быстрые варианты и сноска «Сработает … — через …». Применяется только по ✓.</summary>
public sealed partial class TimeSheetModel : ObservableObject
{
    public IReadOnlyList<QuickTime> QuickTimes { get; } = [new(6, 30), new(7, 0), new(8, 0)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value), nameof(Footnote))]
    public partial int Hour { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value), nameof(Footnote))]
    public partial int Minute { get; set; }

    public TimeOnly Value => new(this.Hour, this.Minute);

    /// <summary>
    /// Когда сработает будильник с этим временем суток. По умолчанию — «Ближайшее» (сегодня или завтра);
    /// редактор подставляет своё правило с датой и повтором.
    /// </summary>
    public Func<TimeOnly, DateTime> Target { get; set; } = time => AlarmSchedule.ResolveNearest(time, DateTime.Now);

    /// <summary>Сноска под барабаном (DESIGN.md §3.2): «Сработает в субботу, 24 октября, — через 20 ч 10 мин.».</summary>
    public string Footnote => AlarmSchedule.Footnote(
        this.Target(this.Value),
        DateTime.Now,
        CultureInfo.CurrentUICulture,
        key => AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key);

    /// <summary>true — ✓ «Готово», false — ✕ «Отменить».</summary>
    public event EventHandler<bool>? Done;

    /// <summary>Ставит время на барабаны; барабаны переходят к нему сами (Value у WheelPicker двусторонний).</summary>
    public void Load(TimeOnly time)
    {
        this.Hour = time.Hour;
        this.Minute = time.Minute;

        // Время то же, а дата или повтор записи могли смениться, пока шторка была закрыта.
        this.RefreshFootnote();
    }

    /// <summary>Пересчитывает сноску: «через сколько» уходит со временем, а барабаны не двигаются.</summary>
    public void RefreshFootnote() => this.OnPropertyChanged(nameof(this.Footnote));

    [RelayCommand]
    private void Quick(QuickTime item) => this.Load(item.Time);

    [RelayCommand]
    private void Now() => this.Load(TimeOnly.FromDateTime(DateTime.Now));

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    // Два вызывающих (Hour, Minute): у быстрых вариантов «вкл» — тот, что совпал с барабанами.
    private void MarkQuickTimes()
    {
        foreach (var quick in this.QuickTimes)
        {
            quick.IsOn = quick.Time == this.Value;
        }
    }

    partial void OnHourChanged(int value) => this.MarkQuickTimes();

    partial void OnMinuteChanged(int value) => this.MarkQuickTimes();
}
