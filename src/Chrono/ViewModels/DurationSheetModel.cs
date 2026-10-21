using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>
/// Черновик шторки длительности: чип из <see cref="Presets"/> или своё значение 1…720 минут.
/// Поле своего значения модель не переписывает, кроме очистки при выборе чипа и заполнения в <see cref="Load"/>.
/// </summary>
public sealed partial class DurationSheetModel : ObservableObject
{
    public const int MaxMinutes = 720;

    public static readonly IReadOnlyList<int> Presets = [0, 15, 30, 45, 60, 90];

    public DurationSheetModel()
    {
        this.Chips = new(Presets.Select(minutes => new ChoiceOption(
            minutes.ToString(CultureInfo.InvariantCulture),
            minutes == 0
                ? AppResources.DurationNone
                : string.Format(AppResources.Culture ?? CultureInfo.CurrentUICulture, AppResources.DurationMinutes, minutes))));
    }

    public ObservableCollection<ChoiceOption> Chips { get; }

    /// <summary>Своё значение. Пустая строка — поле не используется, действует выбранный чип.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string CustomText { get; set; } = string.Empty;

    /// <summary>Итог шторки: своё значение, если оно введено, иначе выбранный чип; 0…720.</summary>
    public int Minutes =>
        this.CustomText.Length > 0
            ? (TryParseCustom(this.CustomText, out var custom) ? custom : 0)
            : int.Parse(this.Chips.FirstOrDefault(c => c.IsSelected)?.Id ?? "0", CultureInfo.InvariantCulture);

    public event EventHandler<bool>? Done;

    public void Load(int minutes)
    {
        minutes = Math.Clamp(minutes, 0, MaxMinutes);
        this.CustomText = string.Empty;
        foreach (var chip in this.Chips)
        {
            chip.IsSelected = chip.Id == minutes.ToString(CultureInfo.InvariantCulture);
        }

        if (!Presets.Contains(minutes))
        {
            this.CustomText = minutes.ToString(CultureInfo.InvariantCulture);
        }

        this.OkCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Только цифры и значение 1…720. <c>Keyboard.Numeric</c> на Android пропускает «.», «,», «-» и пробел;
    /// <c>NumberStyles.None</c> не допускает знак, пробелы и разделители, проверка <c>IsAsciiDigit</c> — подстраховка.
    /// </summary>
    public static bool TryParseCustom(string text, out int minutes) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out minutes)
        && text.All(char.IsAsciiDigit)
        && minutes is >= 1 and <= MaxMinutes;

    [RelayCommand]
    private void SelectPreset(ChoiceOption option)
    {
        this.CustomText = string.Empty;
        foreach (var chip in this.Chips)
        {
            chip.IsSelected = chip == option;
        }

        this.OkCommand.NotifyCanExecuteChanged();
    }

    /// <summary>«Окей» активен при выбранном чипе (поле пусто) или при корректном своём значении.</summary>
    private bool CanOk() =>
        this.CustomText.Length > 0
            ? TryParseCustom(this.CustomText, out _)
            : this.Chips.Any(c => c.IsSelected);

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnCustomTextChanged(string value)
    {
        // Ввод в поле снимает выбор чипа; пустое поле выбор не трогает.
        if (value.Length == 0)
        {
            return;
        }

        foreach (var chip in this.Chips)
        {
            chip.IsSelected = false;
        }
    }
}
