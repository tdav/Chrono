namespace Chrono.Design;

public static class DesignBuilderExtensions
{
    /// <summary>
    /// Дизайн Apple (DESIGN.md): шрифт Inter для Android, системный SF с Dynamic Type для iOS, окраска значков,
    /// цвета нативных контролов и ThemeService. Вызов — в MauiProgram сразу после UseMauiApp.
    /// </summary>
    public static MauiAppBuilder UseChronoDesign(this MauiAppBuilder builder)
    {
        builder.ConfigureFonts(fonts =>
        {
            // Только Android: на iOS стили не задают FontFamily, и текст набирается системным SF Pro.
            fonts.AddFont("Inter-Light.ttf", "InterLight");
            fonts.AddFont("Inter-Regular.ttf", "InterRegular");
            fonts.AddFont("Inter-Medium.ttf", "InterMedium");
            fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
            fonts.AddFont("Inter-Bold.ttf", "InterBold");
        });
        Typography.Register();
        IconTint.Register();
        NativeControls.Register();
        builder.Services.AddSingleton<ThemeService>();
        return builder;
    }
}
