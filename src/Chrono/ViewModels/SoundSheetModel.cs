using System.Collections.ObjectModel;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>
/// Черновик вида «Звук»: Id "" — «Без звука». Выбор мелодии проигрывает её один раз, «Без звука» останавливает прослушивание
/// (DESIGN.md §3.5); <see cref="Options"/> и <see cref="SelectCommand"/> годятся и списку в шторке, и <c>Controls/PopupMenu</c>.
/// </summary>
public sealed partial class SoundSheetModel : ObservableObject
{
    private readonly ISoundPlayer player;

    // Значение, с которым вид открыли: по нему считается IsDirty.
    private string loadedId = Sounds.Default;

    public SoundSheetModel(ISoundPlayer player)
    {
        this.player = player;
        this.Options = new([
            new ChoiceOption("", AppResources.NoSound),
            .. Sounds.All.Select(id => new ChoiceOption(id, AppResources.ResourceManager.GetString($"Sound_{id}", AppResources.Culture) ?? id)),
        ]);
        this.Options[^1].IsLast = true;
        this.OnSelectedIdChanged(this.SelectedId);
    }

    public ObservableCollection<ChoiceOption> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    public partial string SelectedId { get; set; } = Sounds.Default;

    /// <summary>Выбор отличается от загруженного: закрытие без ✓ спросит «Не сохранять / Сохранить / Отменить».</summary>
    public bool IsDirty => this.SelectedId != this.loadedId;

    public event EventHandler<bool>? Done;

    public void Load(bool soundEnabled, string soundId)
    {
        this.loadedId = soundEnabled ? soundId : "";
        this.SelectedId = this.loadedId;

        // SelectedId мог не измениться (то же значение, что и в прошлый раз), а loadedId — да.
        this.OnPropertyChanged(nameof(this.IsDirty));
    }

    [RelayCommand]
    private void Select(ChoiceOption option)
    {
        this.SelectedId = option.Id;
        if (option.Id.Length == 0)
        {
            this.player.Stop();
        }
        else
        {
            this.player.Play(option.Id, loop: false);
        }
    }

    [RelayCommand]
    private void Ok()
    {
        this.player.Stop();
        this.Done?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        this.player.Stop();
        this.Done?.Invoke(this, false);
    }

    partial void OnSelectedIdChanged(string value)
    {
        foreach (var option in this.Options)
        {
            option.IsSelected = option.Id == value;
        }
    }
}
