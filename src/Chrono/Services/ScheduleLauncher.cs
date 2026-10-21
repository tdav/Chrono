namespace Chrono.Services;

/// <summary>
/// Открывает вкладку «Распорядок» на нужном дне (нажатие на уведомление).
/// Если Shell ещё не создан, день запоминается и показывается из AlarmListViewModel.AppearingAsync через ShowPendingAsync.
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
        if (PendingDay is not DateOnly day || Shell.Current is null)
        {
            return;
        }

        PendingDay = null;
        await Shell.Current.GoToAsync($"//schedule?day={day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}");
    }
}
