using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Черновик вида «один выбор из списка» (оповещение, иконка); результат уходит хозяину по «Окей».</summary>
public sealed partial class OptionsSheetModel : ObservableObject
{
    // Значение, с которым вид открыли: по нему считается IsDirty.
    private string loadedId = string.Empty;

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
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    public partial string SelectedId { get; set; } = string.Empty;

    /// <summary>Выбор отличается от загруженного: закрытие без ✓ спросит «Не сохранять / Сохранить / Отменить».</summary>
    public bool IsDirty => this.SelectedId != this.loadedId;

    public event EventHandler<bool>? Done;

    public void Load(string selectedId)
    {
        this.loadedId = selectedId;
        this.SelectedId = selectedId;

        // SelectedId мог не измениться (то же значение, что и в прошлый раз), а loadedId — да.
        this.OnPropertyChanged(nameof(this.IsDirty));
    }

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
