using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>
/// Вид повтора (DESIGN.md §3.4). «По дням недели» без выбранных дней — ошибка: под днями сноска «Выберите хотя бы один день.»,
/// ✓ формы неактивна (<c>AlarmEditViewModel.IsDaysMissing</c>).
/// </summary>
public sealed partial class RepeatSheetModel : ObservableObject
{
    public RepeatSheetModel()
    {
        // Порядок задан явно: enum хранится числом, и новые значения (Monthly) добавляются в его конец, а в списке — между «По дням недели» и «Каждый год».
        this.Options = new(new[] { RepeatKind.None, RepeatKind.Daily, RepeatKind.Weekly, RepeatKind.Monthly, RepeatKind.Yearly, RepeatKind.OnceThenDelete }.Select(kind =>
            new ChoiceOption(kind.ToString(), AppResources.ResourceManager.GetString($"Repeat_{kind}", AppResources.Culture) ?? kind.ToString())));
        this.Options[^1].IsLast = true;

        // Неделя с понедельника; AbbreviatedDayNames начинается с воскресенья.
        // В ru сокращения приходят строчными («пн») — первая буква заглавная.
        var culture = CultureInfo.CurrentUICulture;
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        this.Days = new(Enumerable.Range(1, 7).Select(i => (DayOfWeek)(i % 7))
            .Select(day => new ChoiceOption(((WeekDays)(1 << (int)day)).ToString(), culture.TextInfo.ToUpper(names[(int)day][0]) + names[(int)day][1..])));
        foreach (var day in this.Days)
        {
            day.PropertyChanged += (_, _) => this.OnSelectionChanged();
        }

        this.OnRepeatChanged(this.Repeat);
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    public ObservableCollection<ChoiceOption> Days { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDaysVisible))]
    public partial RepeatKind Repeat { get; set; }

    public bool IsDaysVisible => this.Repeat == RepeatKind.Weekly;

    public WeekDays SelectedDays =>
        this.Days.Where(d => d.IsSelected).Aggregate(WeekDays.None, (days, d) => days | Enum.Parse<WeekDays>(d.Id));

    /// <summary>Итог выбора дней сноской: «Пн, Ср, Пт».</summary>
    public string DaysSummary => string.Join(", ", this.Days.Where(d => d.IsSelected).Select(d => d.Name));

    public bool HasDays => this.SelectedDays != WeekDays.None;

    /// <summary>«По дням недели» без дней: показывается сноска-ошибка.</summary>
    public bool IsDaysEmpty => this.IsDaysVisible && !this.HasDays;

    public void Load(RepeatKind repeat, WeekDays days)
    {
        this.Repeat = repeat;
        foreach (var day in this.Days)
        {
            day.IsSelected = days.HasFlag(Enum.Parse<WeekDays>(day.Id));
        }

        // Повтор мог не измениться (то же значение, что и в прошлый раз), а дни — да.
        this.OnSelectionChanged();
    }

    [RelayCommand]
    private void Select(ChoiceOption option) => this.Repeat = Enum.Parse<RepeatKind>(option.Id);

    [RelayCommand]
    private void ToggleDay(ChoiceOption option) => option.IsSelected = !option.IsSelected;

    partial void OnRepeatChanged(RepeatKind value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value.ToString();
        }

        this.OnSelectionChanged();
    }

    // Три вызывающих (смена повтора, смена любого дня, загрузка): всё, что выводится из повтора и набора дней.
    private void OnSelectionChanged()
    {
        this.OnPropertyChanged(nameof(this.DaysSummary));
        this.OnPropertyChanged(nameof(this.HasDays));
        this.OnPropertyChanged(nameof(this.IsDaysEmpty));
    }
}
