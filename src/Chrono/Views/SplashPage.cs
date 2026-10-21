using Microsoft.Maui.Controls.Shapes;

namespace Chrono.Views;

/// <summary>
/// Вторая ступень splash: тот же фон и колокол, что у нативного MauiSplashScreen, «оживают» ≈0,7 с,
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
        this.Background = new SolidColorBrush(Color.FromArgb("#F5F2EC"));

        // Круг и колокол совпадают с нативным splash (круг r=30 из 108 при BaseSize 128 ≈ 71 dp, колокол ≈34×39 dp) — переход без скачка.
        var disc = new Ellipse
        {
            WidthRequest = 71,
            HeightRequest = 71,
            // TryGetValue ищет и в MergedDictionaries; при отсутствии ключа — сплошной цвет токена, а не исключение.
            Fill = Application.Current!.Resources.TryGetValue("MetalBrush", out var metal) && metal is Brush metalBrush
                ? metalBrush
                : new SolidColorBrush(Color.FromArgb("#2E3136")),
            Stroke = Application.Current.Resources.TryGetValue("GoldEdgeBrush", out var edge) && edge is Brush edgeBrush
                ? edgeBrush
                : new SolidColorBrush(Color.FromArgb("#D1B27C")),
            StrokeThickness = 1.5,
        };
        this.bell = new Image { Source = "bell.png", WidthRequest = 34, HeightRequest = 39, AnchorY = 0.08 };
        this.waves = Enumerable.Range(0, 3).Select(_ => new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromArgb("#D1B27C")),
            StrokeThickness = 2,
            WidthRequest = 160,
            HeightRequest = 160,
            Opacity = 0,
        }).ToArray();
        this.title = new Label
        {
            Text = "Chrono",
            FontFamily = "ManropeExtraBold",
            FontSize = 28,
            TextColor = Color.FromArgb("#2E3136"),
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

        center.Add(disc);
        center.Add(this.bell);
        center.Add(this.title);
        this.Content = center;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Chrono.Services.StartTrace.Mark("SplashPage: показана");

        var swing = async () =>
        {
            await this.bell.RotateToAsync(-16, 110, Easing.SinOut);
            await this.bell.RotateToAsync(14, 200, Easing.SinInOut);
            await this.bell.RotateToAsync(-8, 170, Easing.SinInOut);
            await this.bell.RotateToAsync(0, 140, Easing.SinIn);
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
                await Task.Delay(120);
            }
        };

        await Task.WhenAll(swing(), ripple(), this.title.FadeToAsync(1, 500, Easing.CubicIn));
        // Пауза нужна и для D1 (см. ниже): продолжение должно прийти отдельным сообщением главного цикла.
        await Task.Delay(100);

#if ANDROID
        // Экран выключили, пока шёл splash (активити остановлена): MAUI откладывает транзакции фрагментов нового Shell
        // до возобновления и выполняет их внутри onPostResume → «FragmentManager is already executing transactions»,
        // процесс падает (D1). Ждём возобновления; продолжение после Delay приходит отдельным сообщением главного цикла,
        // то есть уже после onPostResume.
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is AndroidX.Lifecycle.ILifecycleOwner owner
            && !owner.Lifecycle.CurrentState.IsAtLeast(AndroidX.Lifecycle.Lifecycle.State.Resumed))
        {
            // Состояние пишется в журнал: так видно, сколько splash ждал и чем кончилось (диагностика D2).
            var waited = System.Diagnostics.Stopwatch.StartNew();
            global::Android.Util.Log.Info("Chrono", $"splash: ждём возобновления активити, состояние {owner.Lifecycle.CurrentState}");
            for (var tick = 1; !owner.Lifecycle.CurrentState.IsAtLeast(AndroidX.Lifecycle.Lifecycle.State.Resumed); tick++)
            {
                // Активити уничтожена (Назад во время splash): страницу уничтоженному окну не выставляем.
                if (owner.Lifecycle.CurrentState.Equals(AndroidX.Lifecycle.Lifecycle.State.Destroyed))
                {
                    global::Android.Util.Log.Info("Chrono", $"splash: активити уничтожена через {waited.ElapsedMilliseconds} мс, выходим");
                    return;
                }

                await Task.Delay(100);
                // Раз в 2 с и только первую минуту: свёрнутый splash может ждать часами и вытеснил бы журнал.
                if (tick % 20 == 0 && tick <= 600)
                {
                    global::Android.Util.Log.Info("Chrono", $"splash: всё ещё ждём {waited.ElapsedMilliseconds} мс, состояние {owner.Lifecycle.CurrentState}");
                }
            }

            global::Android.Util.Log.Info("Chrono", $"splash: возобновлено через {waited.ElapsedMilliseconds} мс, состояние {owner.Lifecycle.CurrentState}");
        }
#endif

        this.Window!.Page = this.services.GetRequiredService<AppShell>();
        Chrono.Services.StartTrace.Mark("SplashPage: Shell выставлен");
    }
}
