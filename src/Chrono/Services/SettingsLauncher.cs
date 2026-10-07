namespace Chrono.Services;

/// <summary>
/// Открывает экран настроек по запросу системы (Health Connect показывает, зачем приложению данные).
/// Если Shell ещё не создан, запрос выполняет AlarmListPage.OnAppearing через ShowPendingAsync.
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
        if (!Pending || Shell.Current is null)
        {
            return;
        }

        Pending = false;
        await Shell.Current.GoToAsync("settings");
    }
}
