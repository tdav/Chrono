using System.Collections.ObjectModel;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Черновик шторки звука: Id "" — «Без звука». Нажатие на мелодию проигрывает её один раз.</summary>
public sealed partial class SoundSheetModel : ObservableObject
{
    private readonly ISoundPlayer player;

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
    public partial string SelectedId { get; set; } = Sounds.Default;

    public event EventHandler<bool>? Done;

    public void Load(bool soundEnabled, string soundId) => this.SelectedId = soundEnabled ? soundId : "";

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
