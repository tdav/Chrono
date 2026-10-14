using System.Windows.Input;

namespace Chrono.Controls;

/// <summary>Нижняя шторка поверх страницы: затемнение, панель с заголовком и произвольным содержимым.</summary>
public partial class BottomSheet : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), typeof(bool), typeof(BottomSheet), false,
        propertyChanged: (b, _, n) => ((BottomSheet)b).IsVisible = (bool)n);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(BottomSheet), string.Empty);

    public static readonly BindableProperty HintProperty = BindableProperty.Create(
        nameof(Hint), typeof(string), typeof(BottomSheet), string.Empty);

    public static readonly BindableProperty SheetContentProperty = BindableProperty.Create(
        nameof(SheetContent), typeof(View), typeof(BottomSheet), null,
        propertyChanged: (b, _, n) => ((BottomSheet)b).Body.Content = (View?)n);

    public static readonly BindableProperty CancelCommandProperty = BindableProperty.Create(
        nameof(CancelCommand), typeof(ICommand), typeof(BottomSheet), null);

    public BottomSheet()
    {
        this.InitializeComponent();
    }

    public bool IsOpen
    {
        get => (bool)this.GetValue(IsOpenProperty);
        set => this.SetValue(IsOpenProperty, value);
    }

    public string Title
    {
        get => (string)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    public string Hint
    {
        get => (string)this.GetValue(HintProperty);
        set => this.SetValue(HintProperty, value);
    }

    public View? SheetContent
    {
        get => (View?)this.GetValue(SheetContentProperty);
        set => this.SetValue(SheetContentProperty, value);
    }

    /// <summary>Выполняется при нажатии на затемнение.</summary>
    public ICommand? CancelCommand
    {
        get => (ICommand?)this.GetValue(CancelCommandProperty);
        set => this.SetValue(CancelCommandProperty, value);
    }
}
