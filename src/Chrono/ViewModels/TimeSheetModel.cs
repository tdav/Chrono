using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chrono.ViewModels;

/// <summary>Барабаны часов и минут формы и сноска под ними «Сработает … — через …».</summary>
public sealed partial class TimeSheetModel : ObservableObject
{
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
}
