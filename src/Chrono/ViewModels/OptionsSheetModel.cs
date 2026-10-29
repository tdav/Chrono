using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Вид «один выбор из списка» (оповещение, иконка): форма следит за <see cref="SelectedId"/> и применяет выбор сразу.</summary>
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

    public void Load(string selectedId) => this.SelectedId = selectedId;

    [RelayCommand]
    private void Select(ChoiceOption option) => this.SelectedId = option.Id;

    partial void OnSelectedIdChanged(string value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
