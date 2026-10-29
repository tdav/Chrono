using System.Collections;
using System.Windows.Input;
using Chrono.ViewModels;

namespace Chrono.Controls;

/// <summary>
/// Ряд кругов Пн…Вс (DESIGN.md §3.4): можно выбрать несколько дней. Дни и их выбор — в <see cref="Days"/>
/// (<see cref="ChoiceOption.IsSelected"/>), нажатие на круг выполняет <see cref="ToggleCommand"/> с днём в параметре.
/// Итог выбора («Пн, Ср, Пт») и ошибку пустого набора показывает хозяин — сноской под рядом.
/// </summary>
public partial class DayToggles : ContentView
{
    public static readonly BindableProperty DaysProperty = BindableProperty.Create(
        nameof(Days), typeof(IEnumerable), typeof(DayToggles), null);

    public static readonly BindableProperty ToggleCommandProperty = BindableProperty.Create(
        nameof(ToggleCommand), typeof(ICommand), typeof(DayToggles), null);

    public DayToggles()
    {
        this.InitializeComponent();
    }

    /// <summary>Семь дней с понедельника: <see cref="ChoiceOption"/> с названием и признаком выбора.</summary>
    public IEnumerable? Days
    {
        get => (IEnumerable?)this.GetValue(DaysProperty);
        set => this.SetValue(DaysProperty, value);
    }

    /// <summary>Нажатие на круг; параметр — <see cref="ChoiceOption"/> нажатого дня.</summary>
    public ICommand? ToggleCommand
    {
        get => (ICommand?)this.GetValue(ToggleCommandProperty);
        set => this.SetValue(ToggleCommandProperty, value);
    }

    private void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ChoiceOption day && this.ToggleCommand?.CanExecute(day) != false)
        {
            this.ToggleCommand?.Execute(day);
        }
    }
}
