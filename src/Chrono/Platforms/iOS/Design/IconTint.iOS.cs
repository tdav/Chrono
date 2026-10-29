using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace Chrono.Design;

public static partial class IconTint
{
    // Картинка в UIImageView приходит асинхронно; после загрузки (Image.IsLoading → false) её надо снова
    // перевести в шаблонный режим, иначе TintColor её не красит. Подписка — одна на Image.
    private static readonly ConditionalWeakTable<Image, object> Watched = new();

    static partial void RegisterPlatform()
    {
        ImageHandler.Mapper.AppendToMapping(MapperKey, (handler, view) =>
        {
            if (view is not Image image)
            {
                return;
            }

            Apply(handler.PlatformView, GetColor(image));
            if (Watched.TryGetValue(image, out _))
            {
                return;
            }

            Watched.Add(image, new object());
            image.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == Image.IsLoadingProperty.PropertyName && !image.IsLoading
                    && image.Handler?.PlatformView is UIImageView imageView)
                {
                    Apply(imageView, GetColor(image));
                }
            };
        });
    }

    private static void Apply(UIImageView imageView, Color? color)
    {
        imageView.TintColor = color?.ToPlatform();
        if (color is not null && imageView.Image is { } source && source.RenderingMode != UIImageRenderingMode.AlwaysTemplate)
        {
            imageView.Image = source.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
        }
    }
}
