using Chrono.ViewModels;
using Chrono.Views;

namespace Chrono.Services;

/// <summary>
/// Открывает RingPage по запросу платформы (full-screen intent на Android, уведомление на iOS).
/// Запрос живёт в PendingId, пока RingPage действительно не окажется на экране: показ повторяется при каждом
/// поводе — сам запрос, возобновление активити (MainActivity.OnPostResume), любой переход Shell (AppShell.OnNavigated)
/// и появление вкладки «Будильники». Так сигнал не теряется ни на другой вкладке, ни при остановленной активити.
/// </summary>
public static class RingLauncher
{
    // Переход на RingPage уже идёт: повторный повод не должен открыть второй экран сигнала.
    private static bool navigating;

    public static Guid? PendingId { get; private set; }

    public static void Request(Guid alarmId)
    {
        PendingId = alarmId;
        MainThread.BeginInvokeOnMainThread(async () => await ShowPendingAsync());
    }

    public static async Task ShowPendingAsync()
    {
        if (PendingId is not Guid alarmId || navigating || Shell.Current is not Shell shell)
        {
            return;
        }

#if ANDROID
        // Активити не возобновлена (экран выключен, приложение в фоне, холодный старт до onResume): MAUI откладывает
        // транзакции фрагментов Shell, и переход теряется, а на холодном старте ломает отрисовку Shell (пустое окно).
        // Запрос остаётся и выполнится из MainActivity.OnPostResume.
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is AndroidX.Lifecycle.ILifecycleOwner owner
            && !owner.Lifecycle.CurrentState.IsAtLeast(AndroidX.Lifecycle.Lifecycle.State.Resumed!))
        {
            global::Android.Util.Log.Info("Chrono", $"ring: ждём возобновления активити, состояние {owner.Lifecycle.CurrentState}");
            return;
        }
#endif

        // Сигнал уже на экране — новый будильник заменяет текущий, а не открывает второй экран.
        if (shell.CurrentPage is RingPage { BindingContext: RingViewModel ring })
        {
            PendingId = null;
            ring.Load(alarmId);
            return;
        }

        navigating = true;
        try
        {
            await shell.GoToAsync($"ring?id={alarmId}");
        }
        catch (Exception ex)
        {
            // Запрос остаётся в ожидании: следующий повод повторит переход.
#if ANDROID
            global::Android.Util.Log.Error("Chrono", $"ring: переход не выполнен: {ex}");
#endif
        }
        finally
        {
            navigating = false;
        }

        // Снимаем запрос, только если RingPage действительно на экране и за время перехода не пришёл другой сигнал.
        if (shell.CurrentPage is RingPage && PendingId == alarmId)
        {
            PendingId = null;
        }

#if ANDROID
        global::Android.Util.Log.Info("Chrono", $"ring: после перехода на экране {shell.CurrentPage?.GetType().Name}, в ожидании {PendingId?.ToString() ?? "ничего"}");
#endif
    }
}
