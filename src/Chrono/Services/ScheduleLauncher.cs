namespace Chrono.Services;

/// <summary>
/// Открывает вкладку «Распорядок» на нужном дне (нажатие на уведомление).
/// Если Shell ещё не создан или на экране сигнал, день запоминается и показывается позже через ShowPendingAsync
/// (AlarmListViewModel.AppearingAsync, AppShell.OnNavigated, MainActivity.OnPostResume).
/// </summary>
public static class ScheduleLauncher
{
    public static DateOnly? PendingDay { get; private set; }

    public static void Request(DateOnly day)
    {
        PendingDay = day;
        MainThread.BeginInvokeOnMainThread(async () => await ShowPendingAsync());
    }

    public static async Task ShowPendingAsync()
    {
        // Сигнал на экране или ждёт показа: абсолютный переход закрыл бы RingPage. Запрос остаётся и выполнится
        // из AppShell.OnNavigated, когда сигнал закроют.
        if (PendingDay is not DateOnly day || Shell.Current is null
            || RingLauncher.PendingId is not null || Shell.Current.CurrentPage is Views.RingPage)
        {
            return;
        }

        PendingDay = null;
        await Shell.Current.GoToAsync($"//schedule?day={day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}");
    }
}
