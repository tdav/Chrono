using Microsoft.Extensions.Logging;

namespace Chrono;

// Промежуточная версия каркаса (задача 1): только шрифты. Финальная версия — в задаче 7.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Light.ttf", "ManropeLight");
                fonts.AddFont("Manrope-Regular.ttf", "ManropeRegular");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemiBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
