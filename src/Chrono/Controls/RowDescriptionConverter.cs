using System.Globalization;

namespace Chrono.Controls;

/// <summary>
/// Описание строки списка для TalkBack / VoiceOver: «Звук, Без звука». Название строки — <c>ConverterParameter</c>, значение — привязка.
/// Нужна строкам формы, которые нажимаются жестом (<c>TapGestureRecognizer</c>): без описания диктор читает куски строки по отдельности.
/// </summary>
public sealed class RowDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? parameter : $"{parameter}, {value}";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
