using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Кружок акцентного цвета в настройках. <see cref="Key"/> — ключ AccentPalette, null — системный акцент.</summary>
public sealed partial class AccentOption(string? key, string name, IRelayCommand selectCommand) : ObservableObject
{
    public string? Key { get; } = key;

    /// <summary>Название цвета для экранного диктора.</summary>
    public string Name { get; } = name;

    public IRelayCommand SelectCommand { get; } = selectCommand;

    /// <summary>Заливка кружка в текущем виде (светлая, тёмная тема, контраст).</summary>
    [ObservableProperty]
    public partial Color Fill { get; set; } = Colors.Transparent;

    /// <summary>Цвет галочки на заливке.</summary>
    [ObservableProperty]
    public partial Color Glyph { get; set; } = Colors.Transparent;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
