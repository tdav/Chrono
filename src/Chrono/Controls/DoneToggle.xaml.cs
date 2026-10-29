using System.Windows.Input;

namespace Chrono.Controls;

/// <summary>
/// Отметка «сделано» в строке ленты «Распорядка» (DESIGN.md §4.9): круг 24 в области нажатия 44. <see cref="IsDone"/> — состояние,
/// нажатие выполняет <see cref="Command"/> (сам элемент состояние не меняет: его меняет модель и возвращает через привязку).
/// </summary>
public partial class DoneToggle : ContentView
{
    public static readonly BindableProperty IsDoneProperty = BindableProperty.Create(
        nameof(IsDone), typeof(bool), typeof(DoneToggle), false,
        propertyChanged: (b, _, n) =>
        {
            var toggle = (DoneToggle)b;
            toggle.Ring.IsVisible = !(bool)n;
            toggle.Disc.IsVisible = (bool)n;
        });

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(DoneToggle), null);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(DoneToggle), null);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(DoneToggle), string.Empty);

    public DoneToggle() => this.InitializeComponent();

    /// <summary>Запись отмечена сделанной: круг Tint с ✓ вместо кольца.</summary>
    public bool IsDone
    {
        get => (bool)this.GetValue(IsDoneProperty);
        set => this.SetValue(IsDoneProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)this.GetValue(CommandProperty);
        set => this.SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => this.GetValue(CommandParameterProperty);
        set => this.SetValue(CommandParameterProperty, value);
    }

    /// <summary>Описание для TalkBack / VoiceOver («Отметить выполненным» / «Снять отметку»): меняется вместе с состоянием.</summary>
    public string Description
    {
        get => (string)this.GetValue(DescriptionProperty);
        set => this.SetValue(DescriptionProperty, value);
    }
}
