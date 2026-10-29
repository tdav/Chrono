using Chrono.Design;
using Microsoft.Maui.Controls.Shapes;

namespace Chrono.Views;

/// <summary>
/// Вторая ступень splash (DESIGN.md §4.7): фон SystemBackground, колокол Chrono на круге Tint 96 и «Chrono» крупным заголовком;
/// колокол качается ≈0,7 с, затем окно переключается на AppShell.
/// </summary>
public sealed class SplashPage : ContentPage
{
    private readonly IServiceProvider services;
    private readonly Image bell;
    private readonly Label title;

    public SplashPage(IServiceProvider services)
    {
        this.services = services;
        // Цвета и шрифты — из стилей дизайна по DynamicResource: заставка верна и в светлой, и в тёмной теме.
        this.SetDynamicResource(NavigableElement.StyleProperty, "PlainPage");

        var disc = new Border
        {
            WidthRequest = 96,
            HeightRequest = 96,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 48 },
            StrokeThickness = 0,
        };
        disc.SetDynamicResource(BackgroundColorProperty, "Tint");
        // Колокол ≈ 0,43 ширины круга — как на сигнале (48 из 112); ось качания у верха (AnchorY).
        this.bell = new Image
        {
            Source = "bell.png",
            WidthRequest = 41,
            HeightRequest = 47,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            AnchorY = 0.08,
        };
        this.bell.SetDynamicResource(IconTint.ColorProperty, "OnTint");
        this.title = new Label
        {
            Text = "Chrono",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 16, 0, 0),
            Opacity = 0,
        };
        this.title.SetDynamicResource(NavigableElement.StyleProperty, "LargeTitle");

        // Круг стоит в центре экрана, как колокол нативного splash: средняя строка 96 между двумя равными; заголовок — под кругом.
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = new GridLength(96) },
                new RowDefinition { Height = GridLength.Star },
            },
        };
        layout.Add(disc, 0, 1);
        layout.Add(this.bell, 0, 1);
        layout.Add(this.title, 0, 2);
        this.Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Chrono.Services.StartTrace.Mark("SplashPage: показана");

        // «Уменьшение движения» (DESIGN.md §2.9): колокол не качается, заголовок только проявляется.
        var reduceMotion = this.services.GetRequiredService<ThemeService>().ReduceMotion;
        var swing = async () =>
        {
            if (reduceMotion)
            {
                return;
            }

            await this.bell.RotateToAsync(-16, 110, Easing.SinOut);
            await this.bell.RotateToAsync(14, 200, Easing.SinInOut);
            await this.bell.RotateToAsync(-8, 170, Easing.SinInOut);
            await this.bell.RotateToAsync(0, 140, Easing.SinIn);
        };

        await Task.WhenAll(swing(), this.title.FadeToAsync(1, 500, Easing.CubicIn));
        // Пауза нужна и для D1 (см. ниже): продолжение должно прийти отдельным сообщением главного цикла.
        await Task.Delay(100);

#if ANDROID
        // Экран выключили, пока шёл splash (активити остановлена): MAUI откладывает транзакции фрагментов нового Shell
        // до возобновления и выполняет их внутри onPostResume → «FragmentManager is already executing transactions»,
        // процесс падает (D1). Ждём возобновления; продолжение после Delay приходит отдельным сообщением главного цикла,
        // то есть уже после onPostResume.
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is AndroidX.Lifecycle.ILifecycleOwner owner
            && !owner.Lifecycle.CurrentState.IsAtLeast(AndroidX.Lifecycle.Lifecycle.State.Resumed!))
        {
            // Состояние пишется в журнал: так видно, сколько splash ждал и чем кончилось (диагностика D2).
            var waited = System.Diagnostics.Stopwatch.StartNew();
            global::Android.Util.Log.Info("Chrono", $"splash: ждём возобновления активити, состояние {owner.Lifecycle.CurrentState}");
            for (var tick = 1; !owner.Lifecycle.CurrentState.IsAtLeast(AndroidX.Lifecycle.Lifecycle.State.Resumed!); tick++)
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
