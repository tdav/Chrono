using Chrono.ViewModels;
using Chrono.Views;

namespace Chrono.Services;

/// <summary>
/// Открывает RingPage по запросу платформы (full-screen intent на Android, уведомление на iOS).
/// Если Shell ещё не создан (холодный старт, идёт SplashPage), id запоминается и показывается
/// из AlarmListPage.OnAppearing через ShowPendingAsync.
/// </summary>
public static class RingLauncher
{
    public static Guid? PendingId { get; private set; }

    public static void Request(Guid alarmId)
    {
        PendingId = alarmId;
        MainThread.BeginInvokeOnMainThread(async () => await ShowPendingAsync());
    }

    public static async Task ShowPendingAsync()
    {
        if (PendingId is not Guid alarmId || Shell.Current is null)
        {
            return;
        }

        PendingId = null;

        // Сигнал уже на экране — новый будильник заменяет текущий, а не открывает второй экран.
        if (Shell.Current.CurrentPage is RingPage { BindingContext: RingViewModel ring })
        {
            ring.Load(alarmId);
            return;
        }

        await Shell.Current.GoToAsync($"ring?id={alarmId}");
    }
}
