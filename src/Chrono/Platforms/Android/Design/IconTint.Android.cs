using Android.Content.Res;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Chrono.Design;

public static partial class IconTint
{
    static partial void RegisterPlatform()
    {
        // Тинт ImageView переживает смену картинки, поэтому достаточно задать его при подключении и при смене цвета.
        ImageHandler.Mapper.AppendToMapping(MapperKey, (handler, view) =>
        {
            if (view is Image image)
            {
                handler.PlatformView.ImageTintList = GetColor(image) is { } color ? ColorStateList.ValueOf(color.ToPlatform()) : null;
            }
        });
    }
}
