using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class CalendarDayItem : ObservableObject
{
    public CalendarDayItem(CalendarCell cell)
    {
        this.Cell = cell;
        this.Label = cell.Date?.Day.ToString(CultureInfo.CurrentUICulture) ?? "";
    }

    public CalendarCell Cell { get; }

    public string Label { get; }

    public bool IsToday => this.Cell.IsToday;

    public bool IsWeekend => this.Cell.IsWeekend;

    public bool IsDisabled => this.Cell.Date is not null && !this.Cell.IsEnabled;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Черновик шторки даты. Selected == null — «Ближайшее» (сегодня или завтра по времени).</summary>
public sealed partial class CalendarSheetModel : ObservableObject
{
    private DateOnly today;
    private bool allowPast;
    private int year;
    private int month;

    public CalendarSheetModel()
    {
        // Подписи дней с понедельника: AbbreviatedDayNames начинается с воскресенья.
        // В ru сокращения приходят строчными («пн») — первая буква заглавная.
        var culture = CultureInfo.CurrentUICulture;
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        this.WeekDayNames = Enumerable.Range(1, 7).Select(i => culture.TextInfo.ToUpper(names[i % 7][0]) + names[i % 7][1..]).ToArray();
    }

    public ObservableCollection<CalendarDayItem> Cells { get; } = [];

    public IReadOnlyList<string> WeekDayNames { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    public partial DateOnly? Selected { get; set; }

    [ObservableProperty]
    public partial string MonthTitle { get; set; } = "";

    public string Hint => this.Selected is { } date
        ? date.ToString("ddd, d MMMM", CultureInfo.CurrentUICulture)
        : AppResources.NearestHint;

    /// <summary>«Очистить» (= «Ближайшее») не имеет смысла для ежегодного будильника: дата в нём обязательна.</summary>
    [ObservableProperty]
    public partial bool IsClearVisible { get; set; } = true;

    public event EventHandler<bool>? Done;

    public void Load(DateOnly? selected, DateOnly today, bool allowPast)
    {
        this.today = today;
        this.allowPast = allowPast;
        this.IsClearVisible = !allowPast;
        this.Selected = selected;
        var view = selected ?? today;
        this.year = view.Year;
        this.month = view.Month;
        this.Rebuild();
    }

    [RelayCommand]
    private void PrevMonth()
    {
        (this.year, this.month) = this.month == 1 ? (this.year - 1, 12) : (this.year, this.month - 1);
        this.Rebuild();
    }

    [RelayCommand]
    private void NextMonth()
    {
        (this.year, this.month) = this.month == 12 ? (this.year + 1, 1) : (this.year, this.month + 1);
        this.Rebuild();
    }

    [RelayCommand]
    private void Pick(CalendarDayItem item)
    {
        if (item.Cell.Date is null || !item.Cell.IsEnabled)
        {
            return;
        }

        this.Selected = item.Cell.Date;
        foreach (var cell in this.Cells)
        {
            cell.IsSelected = cell.Cell.Date == this.Selected;
        }
    }

    [RelayCommand]
    private void Today()
    {
        this.Selected = this.today;
        this.year = this.today.Year;
        this.month = this.today.Month;
        this.Rebuild();
    }

    [RelayCommand]
    private void Clear()
    {
        this.Selected = null;
        foreach (var cell in this.Cells)
        {
            cell.IsSelected = false;
        }
    }

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    // Четыре вызывающих (Load, листание назад/вперёд, «Сегодня») — общий пересчёт сетки и заголовка.
    private void Rebuild()
    {
        this.Cells.Clear();
        foreach (var cell in AlarmSchedule.Month(this.year, this.month, this.today, this.allowPast))
        {
            this.Cells.Add(new CalendarDayItem(cell) { IsSelected = cell.Date is not null && cell.Date == this.Selected });
        }

        // «MMMM yyyy» без числа — именительный падеж («октябрь 2026»); первая буква заглавная.
        var title = new DateTime(this.year, this.month, 1).ToString("MMMM yyyy", CultureInfo.CurrentUICulture);
        this.MonthTitle = char.ToUpper(title[0], CultureInfo.CurrentUICulture) + title[1..];
    }
}
