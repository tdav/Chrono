using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>
/// Черновик вида длительности (DESIGN.md §3.8): список «Нет», 15…90 мин и степпер «Своё, мин» (5…720, шаг 5).
/// Источник правды один — <see cref="Minutes"/>: список отмечает совпавшее значение, степпер двигает его, подзаголовок строки его показывает.
/// </summary>
public sealed partial class DurationSheetModel : ObservableObject
{
    public const int MaxMinutes = DurationSteps.Max;

    public static readonly IReadOnlyList<int> Presets = [0, 15, 30, 45, 60, 90];

    // Значение, с которым вид открыли: по нему считается IsDirty.
    private int loadedMinutes;

    public DurationSheetModel()
    {
        this.Options = new(Presets.Select(minutes => new ChoiceOption(
            minutes.ToString(CultureInfo.InvariantCulture),
            minutes == 0
                ? AppResources.DurationNone
                : string.Format(AppResources.Culture ?? CultureInfo.CurrentUICulture, AppResources.DurationMinutes, minutes))));
        this.Options[^1].IsLast = true;
        this.OnMinutesChanged(this.Minutes);
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    /// <summary>Итог вида: 0 («Нет») или 1…720; длительность не кратная 5 остаётся как есть, пока степпер её не тронул.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinutesText), nameof(IsDirty), nameof(CanDecrease), nameof(CanIncrease))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseCommand), nameof(IncreaseCommand))]
    public partial int Minutes { get; set; }

    /// <summary>Подзаголовок строки «Своё, мин»: «35 мин» или «Нет».</summary>
    public string MinutesText => this.Minutes == 0
        ? AppResources.DurationNone
        : string.Format(AppResources.Culture ?? CultureInfo.CurrentUICulture, AppResources.DurationMinutes, this.Minutes);

    /// <summary>Значение отличается от загруженного: закрытие без ✓ спросит «Не сохранять / Сохранить / Отменить».</summary>
    public bool IsDirty => this.Minutes != this.loadedMinutes;

    /// <summary>Степпер на границе диапазона: «−» (≤ 5) или «+» (720) неактивна.</summary>
    public bool CanDecrease => DurationSteps.CanDecrease(this.Minutes);

    public bool CanIncrease => DurationSteps.CanIncrease(this.Minutes);

    public event EventHandler<bool>? Done;

    public void Load(int minutes)
    {
        this.loadedMinutes = Math.Clamp(minutes, 0, MaxMinutes);
        this.Minutes = this.loadedMinutes;

        // Minutes мог не измениться (то же значение, что и в прошлый раз), а loadedMinutes — да.
        this.OnPropertyChanged(nameof(this.IsDirty));
    }

    [RelayCommand]
    private void SelectPreset(ChoiceOption option) => this.Minutes = int.Parse(option.Id, CultureInfo.InvariantCulture);

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    private void Decrease() => this.Minutes = DurationSteps.Decrease(this.Minutes);

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    private void Increase() => this.Minutes = DurationSteps.Increase(this.Minutes);

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnMinutesChanged(int value)
    {
        var id = value.ToString(CultureInfo.InvariantCulture);
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == id;
        }
    }
}
