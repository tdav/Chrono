using Chrono.Resources.Strings;
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

    /// <summary>Последняя строка списка: разделитель под ней не нужен. Выставляется до привязки.</summary>
    public bool IsLast { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial bool IsSelected { get; set; }

    /// <summary>Имя для TalkBack / VoiceOver: выбранный пункт читается «Пн, выбрано», остальные — просто названием.</summary>
    public string AccessibleName => this.IsSelected ? string.Format(AppResources.ChoiceSelected, this.Name) : this.Name;
}
