using Microsoft.Maui.Controls.Shapes;

namespace Chrono.Views;

/// <summary>
/// Вторая ступень splash: тот же фон и колокол, что у нативного MauiSplashScreen, «оживают» ≈1,2 с,
/// затем окно переключается на AppShell.
/// </summary>
public sealed class SplashPage : ContentPage
{
    private readonly IServiceProvider services;
    private readonly Image bell;
    private readonly Ellipse[] waves;
    private readonly Label title;

    public SplashPage(IServiceProvider services)
    {
        this.services = services;
        // Сплошной цвет как у нативного splash — переход без вспышки.
        this.Background = new SolidColorBrush(Color.FromArgb("#0A0B1A"));

        // Размер колокола совпадает с нативным splash (тело ≈55 dp), чтобы переход был без скачка.
        this.bell = new Image { Source = "bell.png", WidthRequest = 72, HeightRequest = 82, AnchorY = 0.08 };
        this.waves = Enumerable.Range(0, 3).Select(_ => new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromArgb("#FFB35C")),
            StrokeThickness = 2,
            WidthRequest = 160,
            HeightRequest = 160,
            Opacity = 0,
        }).ToArray();
        this.title = new Label
        {
            Text = "Chrono",
            FontFamily = "ManropeSemiBold",
            FontSize = 30,
            TextColor = Color.FromArgb("#E9EBFF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0,
            TranslationY = 80,
        };

        var center = new Grid { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        foreach (var wave in this.waves)
        {
            center.Add(wave);
        }

        center.Add(this.bell);
        center.Add(this.title);
        this.Content = center;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var swing = async () =>
        {
            await this.bell.RotateToAsync(-16, 150, Easing.SinOut);
            await this.bell.RotateToAsync(14, 260, Easing.SinInOut);
            await this.bell.RotateToAsync(-8, 220, Easing.SinInOut);
            await this.bell.RotateToAsync(0, 180, Easing.SinIn);
        };
        var ripple = async () =>
        {
            for (var i = 0; i < this.waves.Length; i++)
            {
                var wave = this.waves[i];
                wave.Scale = 0.5;
                wave.Opacity = 0.7;
                _ = wave.ScaleToAsync(1.3, 900, Easing.CubicOut);
                _ = wave.FadeToAsync(0, 900, Easing.CubicIn);
                await Task.Delay(180);
            }
        };

        await Task.WhenAll(swing(), ripple(), this.title.FadeToAsync(1, 700, Easing.CubicIn));
        await Task.Delay(250);

        this.Window!.Page = this.services.GetRequiredService<AppShell>();
    }
}
