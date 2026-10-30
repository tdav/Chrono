using Chrono.Design;

namespace Chrono.Views;

/// <summary>
/// Заставка (DESIGN.md §4.7) — одна: системная заставка показывает её первый кадр, а страница продолжает из него
/// полноэкранную анимацию SplashDrawable ≈ 2,1 с, затем окно переключается на AppShell.
/// Фон чёрный в любой теме (заставка намеренно вне темы: системная знает только свою); рисунок — от края до края окна,
/// чтобы центр совпал с центром значка системной заставки.
/// </summary>
public sealed class SplashPage : ContentPage
{
    private const string AnimationName = "ChronoSplash";

    private readonly IServiceProvider services;
    private readonly SplashDrawable scene;
    private readonly GraphicsView canvas;

    public SplashPage(IServiceProvider services)
    {
        this.services = services;
        this.BackgroundColor = Colors.Black;
        this.SafeAreaEdges = SafeAreaEdges.None;
        this.scene = new SplashDrawable();
        this.canvas = new GraphicsView { Drawable = this.scene };
        this.Content = this.canvas;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Chrono.Services.StartTrace.Mark("SplashPage: показана");

        // «Уменьшение движения» (DESIGN.md §2.9): сразу итоговая композиция, без вращения, стекания и перебора; ~0,6 с — и переход.
        var reduceMotion = this.services.GetRequiredService<ThemeService>().ReduceMotion;
        if (reduceMotion)
        {
            this.scene.Time = SplashDrawable.Composed;
            this.canvas.Invalidate();
        }

        // Отсчёт — с первого нарисованного кадра: до него на экране системная заставка с тем же кадром, и начало анимации
        // не должно уйти на время её показа.
        await this.scene.FirstFrame;
        if (reduceMotion)
        {
            await Task.Delay(600);
        }
        else
        {
            var done = new TaskCompletionSource();
            new Animation(progress =>
            {
                this.scene.Time = (float)progress * SplashDrawable.Duration;
                this.canvas.Scale = this.scene.Zoom;
                this.canvas.Invalidate();
            }).Commit(this, AnimationName, 16, (uint)(SplashDrawable.Duration * 1000), Easing.Linear, (_, _) => done.TrySetResult());
            await done.Task;
        }

#if ANDROID
        // Сцена растворилась в фон приложения: значки панелей — снова по теме (на чёрной заставке они светлые, см. MainActivity).
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is { Window: { } window, Resources.Configuration: { } config })
        {
            ModalSystemBars.Apply(window, config);
        }
#endif

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

    // Уход со страницы (окно получило Shell или закрывается): анимация больше не тикает.
    protected override void OnDisappearing()
    {
        this.AbortAnimation(AnimationName);
        base.OnDisappearing();
    }
}
