using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Черновик шторки повтора; без выбранных дней «По дням недели» сохраняется как «Каждый день».</summary>
public sealed partial class RepeatSheetModel : ObservableObject
{
    public RepeatSheetModel()
    {
        this.Options = new(Enum.GetValues<RepeatKind>().Select(kind =>
            new ChoiceOption(kind.ToString(), AppResources.ResourceManager.GetString($"Repeat_{kind}", AppResources.Culture) ?? kind.ToString())));

        // Неделя с понедельника; AbbreviatedDayNames начинается с воскресенья.
        // В ru сокращения приходят строчными («пн») — первая буква заглавная.
        var culture = CultureInfo.CurrentUICulture;
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        this.Days = new(Enumerable.Range(1, 7).Select(i => (DayOfWeek)(i % 7))
            .Select(day => new ChoiceOption(((WeekDays)(1 << (int)day)).ToString(), culture.TextInfo.ToUpper(names[(int)day][0]) + names[(int)day][1..])));
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

    public event EventHandler<bool>? Done;

    public void Load(RepeatKind repeat, WeekDays days)
    {
        this.Repeat = repeat;
        foreach (var day in this.Days)
        {
            day.IsSelected = days.HasFlag(Enum.Parse<WeekDays>(day.Id));
        }
    }

    [RelayCommand]
    private void Select(ChoiceOption option) => this.Repeat = Enum.Parse<RepeatKind>(option.Id);

    [RelayCommand]
    private void ToggleDay(ChoiceOption option) => option.IsSelected = !option.IsSelected;

    [RelayCommand]
    private void Ok()
    {
        if (this.Repeat == RepeatKind.Weekly && this.SelectedDays == WeekDays.None)
        {
            this.Repeat = RepeatKind.Daily;
        }

        this.Done?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnRepeatChanged(RepeatKind value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value.ToString();
        }
    }
}
