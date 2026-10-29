using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class CalendarDayItem : ObservableObject
{
    // index — позиция в сетке месяца: по ней InlineCalendar ставит ячейку в строку и столбец (неделя с понедельника, 7 столбцов).
    public CalendarDayItem(CalendarCell cell, int index = 0)
    {
        this.Cell = cell;
        this.Label = cell.Date?.Day.ToString(CultureInfo.CurrentUICulture) ?? "";
        this.Row = index / 7;
        this.Column = index % 7;
    }

    public CalendarCell Cell { get; }

    public string Label { get; }

    public bool IsToday => this.Cell.IsToday;

    public bool IsDisabled => this.Cell.Date is not null && !this.Cell.IsEnabled;

    public int Row { get; }

    public int Column { get; }

    // Вид ячейки (DESIGN.md §3.3) — взаимоисключающие состояния, чтобы разметке не зависеть от порядка триггеров:
    // «сегодня» и выбранный день — Semibold; выбранный — круг TintFill, выбранное «сегодня» — круг Tint.
    public bool IsEmphasized => this.IsToday || this.IsSelected;

    public bool IsPlain => !this.IsEmphasized;

    public bool IsSelectedToday => this.IsSelected && this.IsToday;

    public bool IsSelectedOther => this.IsSelected && !this.IsToday;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmphasized), nameof(IsPlain), nameof(IsSelectedToday), nameof(IsSelectedOther))]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Модель календаря: обслуживает <c>Controls/InlineCalendar</c> (чип в строке и календарь в форме, DESIGN.md §3.3).
/// Selected == null — «Ближайшее» (сегодня или завтра по времени) или «Не выбрано» (<see cref="EmptyChipText"/>).
/// Выбор применяется сразу: форма следит за <see cref="Selected"/>, кнопок «Ок» и «Отмена» у календаря в форме нет.
/// </summary>
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
    [NotifyPropertyChangedFor(nameof(ChipText))]
    public partial DateOnly? Selected { get; set; }

    [ObservableProperty]
    public partial string MonthTitle { get; set; } = "";

    /// <summary>Значение чипа строки, пока дата не выбрана; им же подписана кнопка сброса под календарём: «Ближайшее» для даты записи, «Не выбрано» для окончания.</summary>
    public string EmptyChipText { get; init; } = AppResources.Nearest;

    /// <summary>У ежегодного будильника год и день недели не нужны: чип показывает «17 октября» (DESIGN.md §4.1).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChipText))]
    public partial bool IsYearly { get; set; }

    /// <summary>Значение чипа строки (§3.3): «сб, 24 окт.» — с годом, если он не текущий, — или <see cref="EmptyChipText"/>.</summary>
    public string ChipText => this.Selected is { } date
        ? this.IsYearly
            ? date.ToString("d MMMM", CultureInfo.CurrentUICulture)
            : date.ToString(date.Year == this.today.Year ? "ddd, d MMM" : "ddd, d MMM yyyy", CultureInfo.CurrentUICulture)
        : this.EmptyChipText;

    /// <summary>«Очистить» (= «Ближайшее») не имеет смысла для ежегодного будильника: дата в нём обязательна.</summary>
    [ObservableProperty]
    public partial bool IsClearVisible { get; set; } = true;

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

    // Четыре вызывающих (Load, листание по месяцам, «Сегодня») — общий пересчёт сетки и заголовка.
    private void Rebuild()
    {
        this.Cells.Clear();
        var index = 0;
        foreach (var cell in AlarmSchedule.Month(this.year, this.month, this.today, this.allowPast))
        {
            this.Cells.Add(new CalendarDayItem(cell, index++) { IsSelected = cell.Date is not null && cell.Date == this.Selected });
        }

        // «MMMM yyyy» без числа — именительный падеж («октябрь 2026»); первая буква заглавная.
        var title = new DateTime(this.year, this.month, 1).ToString("MMMM yyyy", CultureInfo.CurrentUICulture);
        this.MonthTitle = char.ToUpper(title[0], CultureInfo.CurrentUICulture) + title[1..];
    }
}
