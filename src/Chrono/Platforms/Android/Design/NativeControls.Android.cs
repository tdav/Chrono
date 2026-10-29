using Android.Content.Res;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Chrono.Design;

internal static partial class NativeControls
{
    static partial void RegisterPlatform()
    {
        // Тумблер (DESIGN.md §3 › Тумблер): «вкл» — Success, «выкл» — SecondarySystemFill, бегунок — Knob.
        // Вешаем на ключи MAUI (OnColor, ThumbColor, IsOn): их собственные маппинги сбрасывают тинт.
        // Цвета читаются в момент маппинга, поэтому тумблер получает цвета текущего вида при каждом переключении.
        foreach (var key in new[] { "OnColor", "ThumbColor", "IsOn" })
        {
            SwitchHandler.Mapper.AppendToMapping(key, (handler, _) =>
            {
                handler.PlatformView.TrackTintList = new ColorStateList(
                    [[global::Android.Resource.Attribute.StateChecked], []],
                    [Resource("Success").ToPlatform().ToArgb(), Resource("SecondarySystemFill").ToPlatform().ToArgb()]);
                handler.PlatformView.ThumbTintList = ColorStateList.ValueOf(Resource("Knob").ToPlatform());
            });
        }
    }
}
