using Microsoft.Maui.Handlers;

namespace Chrono.Design;

public static partial class Typography
{
    static partial void RegisterPlatform()
    {
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
        {
            if (view is not Label label)
            {
                return;
            }

            // Только текст в новых стилях (Inter): у ещё не перенесённых экранов Manrope с локальным кеглем — их не трогаем.
            var style = GetStyle(label);
            if (style == TextStyle.None || label.FontFamily?.StartsWith("Inter", StringComparison.Ordinal) != true)
            {
                return;
            }

            // Высота строки — как на iOS: без запаса TextView под высокие знаки шрифта, иначе строки списка выше макета.
            handler.PlatformView.SetIncludeFontPadding(false);
            // Время и обратный отсчёт — табличными цифрами Inter (tnum), чтобы ширина не прыгала.
            handler.PlatformView.FontFeatureSettings = TextStyles.Get(style).Tabular ? "'tnum'" : null;
        });
    }
}
