using System.Runtime.InteropServices;
using Microsoft.Maui.Handlers;
using UIKit;

namespace Chrono.Design;

public static partial class Typography
{
    /// <summary>
    /// Системный SF для стиля: нужный вес, рост по кривой своего текстового стиля Apple (все 12 размеров Dynamic Type)
    /// с потолком MaxSize; у времени — табличные цифры. Трекинг и интерлиньяж SF система ставит сама.
    /// </summary>
    public static UIFont FontFor(TextStyle style, bool autoScale)
    {
        var metrics = TextStyles.Get(style);
        var weight = (metrics.Weight switch
        {
            TextWeight.Light => UIFontWeight.Light,
            TextWeight.Medium => UIFontWeight.Medium,
            TextWeight.Semibold => UIFontWeight.Semibold,
            TextWeight.Bold => UIFontWeight.Bold,
            _ => UIFontWeight.Regular,
        }).GetWeight();
        // Системный шрифт UIKit возвращает всегда; null в аннотациях привязки — формальность.
        var font = (metrics.Tabular
            ? UIFont.MonospacedDigitSystemFontOfSize((NFloat)metrics.Size, weight)
            : UIFont.SystemFontOfSize((NFloat)metrics.Size, weight))!;
        if (!autoScale)
        {
            return font;
        }

        UIFontTextStyle textStyle = metrics.Scale switch
        {
            DynamicType.LargeTitle => UIFontTextStyle.LargeTitle,
            DynamicType.Title1 => UIFontTextStyle.Title1,
            DynamicType.Title2 => UIFontTextStyle.Title2,
            DynamicType.Title3 => UIFontTextStyle.Title3,
            DynamicType.Headline => UIFontTextStyle.Headline,
            DynamicType.Callout => UIFontTextStyle.Callout,
            DynamicType.Subheadline => UIFontTextStyle.Subheadline,
            DynamicType.Footnote => UIFontTextStyle.Footnote,
            DynamicType.Caption1 => UIFontTextStyle.Caption1,
            DynamicType.Caption2 => UIFontTextStyle.Caption2,
            _ => UIFontTextStyle.Body,
        };
        // Рост по кривой своего стиля Apple: крупные стили растут медленнее мелких, как в системе.
        var scale = UIFontMetrics.GetMetrics(textStyle.GetConstant()!);
        return metrics.MaxSize is { } max ? scale.GetScaledFont(font, (NFloat)max) : scale.GetScaledFont(font);
    }

    static partial void RegisterPlatform()
    {
        // Ключ Font: маппинг MAUI ставит шрифт из FontSize/FontAttributes, наш — сразу после него заменяет его на SF стиля.
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
        {
            // FormattedText: шрифты задают Span, общий шрифт UILabel их бы затёр.
            if (view is Label label && label.FormattedText is null && Leading(label, label.FontFamily, label.FontSize) is { } style)
            {
                handler.PlatformView.Font = FontFor(style, label.FontAutoScalingEnabled);
                handler.PlatformView.AdjustsFontForContentSizeCategory = label.FontAutoScalingEnabled;
            }
        });
        EntryHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
        {
            if (view is Entry entry && Leading(entry, entry.FontFamily, entry.FontSize) is { } style)
            {
                handler.PlatformView.Font = FontFor(style, entry.FontAutoScalingEnabled);
                handler.PlatformView.AdjustsFontForContentSizeCategory = entry.FontAutoScalingEnabled;
            }
        });
    }
}
