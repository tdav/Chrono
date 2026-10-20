using System.Globalization;
using Chrono.Models;

namespace Chrono.Views.Sheets;

/// <summary>Содержимое шторки иконки; модель приходит через BindingContext.</summary>
public partial class IconSheet : ContentView
{
    public IconSheet()
    {
        this.InitializeComponent();
    }
}

/// <summary>Ключ иконки дела → картинка из <see cref="ItemIcons"/>.</summary>
public sealed class IconImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ImageSource.FromFile(ItemIcons.ImageFor(value as string ?? string.Empty));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
