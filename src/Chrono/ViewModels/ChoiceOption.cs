using CommunityToolkit.Mvvm.ComponentModel;

namespace Chrono.ViewModels;

/// <summary>Чип выбора: мелодия, вид повтора или день недели.</summary>
public sealed partial class ChoiceOption : ObservableObject
{
    public ChoiceOption(string id, string name)
    {
        this.Id = id;
        this.Name = name;
    }

    public string Id { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
