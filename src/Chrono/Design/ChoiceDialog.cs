#if ANDROID
using Google.Android.Material.Dialog;
using Microsoft.Maui.Platform;
#endif

namespace Chrono.Design;

/// <summary>Модальный вопрос с кнопками справа: главная — крайняя справа, «Отменить» — слева от неё (правило пользователя для модальных окон).</summary>
public static class ChoiceDialog
{
    /// <summary>
    /// Параметры — как у <see cref="Page.DisplayActionSheetAsync(string, string, string, string[])"/>: на iOS это и есть системный action sheet.
    /// На Android — диалог Material в цветах темы: Positive — <paramref name="action"/> (или <paramref name="destruction"/>, если главного нет),
    /// Negative — <paramref name="cancel"/>, Neutral слева — <paramref name="destruction"/> при наличии главного («Не сохранять»).
    /// Возвращает текст нажатой кнопки; «Назад» и касание вне диалога — <paramref name="cancel"/>.
    /// </summary>
    public static async Task<string> ShowAsync(Page page, string? title, string cancel, string? destruction, string? action = null)
    {
#if ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is { } activity)
        {
            var primary = action ?? destruction ?? cancel;
            // Продолжение (сохранение, навигация) — после обработчика клика диалога, а не внутри него.
            var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var builder = new MaterialAlertDialogBuilder(activity);
            if (title is not null)
            {
                builder.SetTitle(title);
            }

            builder.SetPositiveButton(primary, (_, _) => answer.TrySetResult(primary));
            builder.SetNegativeButton(cancel, (_, _) => answer.TrySetResult(cancel));
            if (action is not null && destruction is not null)
            {
                builder.SetNeutralButton(destruction, (_, _) => answer.TrySetResult(destruction));
            }

            var dialog = builder.Create()!;
            // Закрытие приходит после нажатия кнопки, поэтому TrySetResult выше уже сработал; здесь — только выходы без кнопки.
            dialog.DismissEvent += (_, _) => answer.TrySetResult(cancel);
            dialog.Show();

            // Тема активити MAUI — не Material, и диалог рисовался серой прямоугольной коробкой с белыми кнопками:
            // поверхность — непрозрачное крупное стекло со скруглением RadiusAlert (10, одно на все модальные окна),
            // кнопки — цвет акцента, их подсветка — с теми же углами. Удаление не красное: человек сам его выбрал
            // (DESIGN.md §3 › Алерт).
            var density = activity.Resources!.DisplayMetrics!.Density;
            var corner = (float)(double)Application.Current!.Resources["RadiusAlert"] * density;
            var surface = new Android.Graphics.Drawables.GradientDrawable();
            surface.SetCornerRadius(corner);
            surface.SetColor(NativeControls.Resource("GlassOpaque").ToPlatform());
            dialog.Window?.SetBackgroundDrawable(new Android.Graphics.Drawables.InsetDrawable(surface, (int)(16 * density)));
            var tint = NativeControls.Resource("Tint").ToPlatform();
            var shape = Google.Android.Material.Shape.ShapeAppearanceModel.InvokeBuilder().SetAllCornerSizes(corner).Build();
            foreach (var type in new[] { Android.Content.DialogButtonType.Positive, Android.Content.DialogButtonType.Negative, Android.Content.DialogButtonType.Neutral })
            {
                var button = dialog.GetButton((int)type);
                button?.SetTextColor(tint);
                // Разрядка букв Material (≈0.09 em) делала подписи шире, чем в дизайн-системе.
                button?.LetterSpacing = 0;
                if (button is Google.Android.Material.Button.MaterialButton material)
                {
                    material.ShapeAppearanceModel = shape;
                }
            }

            return await answer.Task;
        }
#endif
        return await page.DisplayActionSheetAsync(title, cancel, destruction, action is null ? [] : [action]) ?? cancel;
    }
}
