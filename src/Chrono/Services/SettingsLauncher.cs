namespace Chrono.Services;

/// <summary>
/// Открывает экран настроек по запросу системы (Health Connect показывает, зачем приложению данные).
/// Если Shell ещё не создан или на экране сигнал, запрос выполняется позже через ShowPendingAsync
/// (AlarmListViewModel.AppearingAsync, AppShell.OnNavigated, MainActivity.OnPostResume).
/// </summary>
public static class SettingsLauncher
{
    public static bool Pending { get; private set; }

    public static void Request()
    {
        Pending = true;
        MainThread.BeginInvokeOnMainThread(async () => await ShowPendingAsync());
    }

    public static async Task ShowPendingAsync()
    {
        // Сигнал на экране или ждёт показа: абсолютный переход закрыл бы RingPage. Запрос остаётся и выполнится
        // из AppShell.OnNavigated, когда сигнал закроют.
        if (!Pending || Shell.Current is null
            || RingLauncher.PendingId is not null || Shell.Current.CurrentPage is Views.RingPage)
        {
            return;
        }

        Pending = false;
        await Shell.Current.GoToAsync("//settings");
    }
}
