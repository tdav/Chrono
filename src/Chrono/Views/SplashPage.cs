using Chrono.Design;

namespace Chrono.Views;

/// <summary>
/// Заставка (DESIGN.md §4.7) — одна: системная заставка показывает её первый кадр, а страница продолжает из него
/// полноэкранную анимацию SplashDrawable; итоговая композиция переходит в слой поверх окна, окно переключается на AppShell,
/// и заставка растворяется в готовый экран.
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

        // «Уменьшение движения» (DESIGN.md §2.9): сразу итоговая композиция, без вращения, стекания и перебора; ~0,6 с — и растворение.
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
            // Сцена идёт по своим часам с шагом не больше 50 мс: если главный поток занят (сборка мусора, загрузка сборок),
            // анимация приостанавливается, а не перескакивает к концу — её видно целиком. Конец — AbortAnimation из самого
            // тика; продолжение отдельным сообщением, а не внутри его цикла снятия анимаций.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var last = TimeSpan.Zero;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            new Animation(_ =>
            {
                var elapsed = clock.Elapsed;
                this.scene.Time = Math.Min(this.scene.Time + (float)Math.Min((elapsed - last).TotalSeconds, 0.05), SplashDrawable.Composed);
                last = elapsed;
                this.canvas.Invalidate();
                if (this.scene.Time >= SplashDrawable.Composed)
                {
                    this.AbortAnimation(AnimationName);
                }
            }).Commit(this, AnimationName, 16, 60_000, Easing.Linear, (_, _) => done.TrySetResult());
            await done.Task;
        }

        var window = this.Window!;
        var shell = this.services.GetRequiredService<AppShell>();

#if ANDROID
        // Итоговая композиция переходит в слой поверх всего окна активити (DecorView): под ним выставляется Shell, рисует
        // первую страницу и достраивает остальные вкладки (≈2 с в отладочной сборке на эмуляторе), а растворяется заставка
        // уже в готовый экран, а не в пустой. Слой — тот же PlatformGraphicsView, что у GraphicsView, и та же сцена; он от края
        // до края окна, как страница, поэтому кадр не сдвигается. Clickable: касания под слоем не проходят, пока он на экране.
        // WindowOverlay из MAUI не годится: после смены страницы окна он больше не перерисовывается.
        Android.Views.View? layer = null;
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.DecorView is Android.Views.ViewGroup decor)
        {
            layer = new Microsoft.Maui.Graphics.Platform.PlatformGraphicsView(decor.Context!, this.scene) { Clickable = true };
            decor.AddView(layer, new Android.Views.ViewGroup.LayoutParams(
                Android.Views.ViewGroup.LayoutParams.MatchParent, Android.Views.ViewGroup.LayoutParams.MatchParent));
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

        window.Page = shell;
        Chrono.Services.StartTrace.Mark("SplashPage: Shell выставлен");

#if ANDROID
        if (layer is not null)
        {
            // Растворение — когда главный поток свободен: начатое во время достройки вкладок оно застыло бы на полпути.
            // Свободен — если 150 мс подряд отвечает без задержек; ждём не дольше 4 с. «Уменьшение движения» тоже
            // растворяется: это смена прозрачности, а не движение.
            var waiting = System.Diagnostics.Stopwatch.StartNew();
            var calm = System.Diagnostics.Stopwatch.StartNew();
            while (calm.ElapsedMilliseconds < 150 && waiting.ElapsedMilliseconds < 4000)
            {
                var asked = waiting.Elapsed;
                await Task.Delay(16);
                if ((waiting.Elapsed - asked).TotalMilliseconds > 50)
                {
                    calm.Restart();
                }
            }

            // Прозрачность всего слоя (а не каждой линии) — без просвечивания наложений; WithLayer — плавно на RenderThread.
            var gone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            layer.Animate()!.Alpha(0).SetDuration(300).WithLayer()!.WithEndAction(new Java.Lang.Runnable(() => gone.TrySetResult()));
            await gone.Task;
            (layer.Parent as Android.Views.ViewGroup)?.RemoveView(layer);
            Chrono.Services.StartTrace.Mark("SplashPage: заставка растворилась");
        }

        // Заставки на экране больше нет: значки панелей — снова по теме (на чёрной заставке они светлые, см. MainActivity).
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is { Window: { } platformWindow, Resources.Configuration: { } config })
        {
            ModalSystemBars.Apply(platformWindow, config);
        }
#endif
    }

    // Уход со страницы (окно получило Shell или закрывается): анимация больше не тикает.
    protected override void OnDisappearing()
    {
        this.AbortAnimation(AnimationName);
        base.OnDisappearing();
    }
}
