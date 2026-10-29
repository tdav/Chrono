using Foundation;
using UIKit;

namespace Chrono.Design;

public static partial class SystemAccessibility
{
    public static partial bool IsHighContrast() => UIAccessibility.DarkerSystemColorsEnabled;

    public static partial bool IsReduceMotion() => UIAccessibility.IsReduceMotionEnabled;

    public static partial bool IsReduceTransparency() => UIAccessibility.IsReduceTransparencyEnabled;

    public static partial IDisposable? Observe(Action changed)
    {
        NSObject[] tokens =
        [
            UIApplication.Notifications.ObserveDarkerSystemColorsStatusDidChange((_, _) => changed()),
            UIApplication.Notifications.ObserveReduceMotionStatusDidChange((_, _) => changed()),
            UIApplication.Notifications.ObserveReduceTransparencyStatusDidChange((_, _) => changed()),
        ];
        return new Subscription(() =>
        {
            foreach (var token in tokens)
            {
                token.Dispose();
            }
        });
    }
}
