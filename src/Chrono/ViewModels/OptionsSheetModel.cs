using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Черновик шторки «один выбор из списка» (оповещение, иконка); результат уходит хозяину по «Окей».</summary>
public sealed partial class OptionsSheetModel : ObservableObject
{
    public OptionsSheetModel(IEnumerable<ChoiceOption> options)
    {
        this.Options = new(options);
        if (this.Options.Count > 0)
        {
            this.Options[^1].IsLast = true;
        }
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    [ObservableProperty]
    public partial string SelectedId { get; set; } = string.Empty;

    public event EventHandler<bool>? Done;

    public void Load(string selectedId) => this.SelectedId = selectedId;

    [RelayCommand]
    private void Select(ChoiceOption option) => this.SelectedId = option.Id;

    [RelayCommand]
    private void Ok() => this.Done?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => this.Done?.Invoke(this, false);

    partial void OnSelectedIdChanged(string value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
