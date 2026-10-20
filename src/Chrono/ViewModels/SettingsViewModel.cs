using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Caching.Memory;

namespace Chrono.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISmartWake smartWake;
    private readonly AlarmService alarmService;
    private readonly IGoogleAuth auth;
    private readonly GoogleCalendarClient google;
    private readonly SyncStore syncStore;
    private readonly SyncLauncher syncLauncher;
    private readonly IMemoryCache cache;

    public SettingsViewModel(
        ISmartWake smartWake,
        AlarmService alarmService,
        IGoogleAuth auth,
        GoogleCalendarClient google,
        SyncStore syncStore,
        SyncLauncher syncLauncher,
        IMemoryCache cache)
    {
        this.smartWake = smartWake;
        this.alarmService = alarmService;
        this.auth = auth;
        this.google = google;
        this.syncStore = syncStore;
        this.syncLauncher = syncLauncher;
        this.cache = cache;
        this.smartWakeEnabled = smartWake.IsSupported && SmartWakeSettings.Enabled;
        this.mathChallengeEnabled = DismissSettings.MathChallenge;
    }

    public bool IsSmartWakeSupported => this.smartWake.IsSupported;

    // Поле, а не partial-свойство: начальное значение задаётся без вызова OnSmartWakeEnabledChanged.
    [ObservableProperty]
    private bool smartWakeEnabled;

    // Поле по той же причине: начальное значение не должно вызывать OnMathChallengeEnabledChanged.
    [ObservableProperty]
    private bool mathChallengeEnabled;

    [ObservableProperty]
    public partial string AccessText { get; set; } = "";

    [ObservableProperty]
    public partial bool CanGrantAccess { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    [NotifyPropertyChangedFor(nameof(IsSignedOut))]
    public partial string? GoogleEmail { get; set; }

    public bool IsSignedIn => this.GoogleEmail is not null;

    public bool IsSignedOut => this.GoogleEmail is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSyncStatus))]
    public partial string SyncStatusText { get; set; } = "";

    public bool HasSyncStatus => this.SyncStatusText.Length > 0;

    /// <summary>Подписка на состояние синхронизации; идемпотентна, снимается в Detach (лаунчер живёт дольше страницы).</summary>
    public void Attach()
    {
        this.syncLauncher.StatusChanged -= this.OnSyncStatusChanged;
        this.syncLauncher.StatusChanged += this.OnSyncStatusChanged;
    }

    public void Detach() => this.syncLauncher.StatusChanged -= this.OnSyncStatusChanged;

    // StatusChanged приходит и с потока таймера, а свойства привязаны к интерфейсу.
    private void OnSyncStatusChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => this.ShowStatus(this.syncLauncher.Status));

    private void ShowStatus(SyncStatus status)
    {
        var state = this.syncStore.Load();
        this.GoogleEmail = state.Email;
        this.SyncStatusText = state.Email is null
            ? ""
            : status switch
            {
                SyncStatus.NoNetwork => AppResources.SyncNoNetwork,
                SyncStatus.SignInRequired => AppResources.SyncSignInAgain,
                _ when state.LastSyncUtc is { } last =>
                    string.Format(AppResources.SyncedAt, DateTime.SpecifyKind(last, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm")),
                _ => "",
            };
    }

    public async Task RefreshAsync()
    {
        this.Attach();
        this.ShowStatus(this.syncLauncher.Status);

        SmartWakeAccess access;
        try
        {
            access = await this.smartWake.GetAccessAsync();
        }
        catch (Exception)
        {
            // Сбой Health Connect не должен ронять экран: показываем как «нет доступа».
            access = SmartWakeAccess.NoPermission;
        }

        this.AccessText = access switch
        {
            SmartWakeAccess.Granted => AppResources.SmartWakeAccessGranted,
            SmartWakeAccess.NoPermission => AppResources.SmartWakeAccessNoPermission,
            SmartWakeAccess.HealthConnectMissing => AppResources.SmartWakeAccessMissing,
            _ => AppResources.SmartWakeUnsupported,
        };
        this.CanGrantAccess = this.SmartWakeEnabled && access is SmartWakeAccess.NoPermission or SmartWakeAccess.HealthConnectMissing;
    }

    partial void OnSmartWakeEnabledChanged(bool value)
    {
        SmartWakeSettings.Enabled = value;

        // Окна умного пробуждения ставятся или снимаются у всех будильников сразу.
        this.alarmService.RescheduleAll();

        // Включение сразу запрашивает все разрешения (после анимации переключателя).
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (value)
            {
                await this.smartWake.RequestAccessAsync();
            }

            await this.RefreshAsync();
        });
    }

    partial void OnMathChallengeEnabledChanged(bool value) => DismissSettings.MathChallenge = value;

    [RelayCommand]
    private async Task GrantAccessAsync()
    {
        await this.smartWake.RequestAccessAsync();
        await this.RefreshAsync();
    }

    [RelayCommand]
    private Task OpenReliabilityAsync() => Shell.Current.GoToAsync("reliability");

    [RelayCommand]
    private async Task SignInGoogleAsync()
    {
        try
        {
            // Окно согласия Google; null — пользователь закрыл его.
            if (await this.auth.GetTokenAsync(true, CancellationToken.None) is null)
            {
                return;
            }

            var email = await this.google.GetEmailAsync(CancellationToken.None);
            this.syncStore.Save(this.syncStore.Load() with { Email = email });
        }
        catch (Exception ex)
        {
            this.ShowStatus(SyncLauncher.StatusOf(ex));
            return;
        }

#if ANDROID
        Platform.SyncWorker.Schedule(global::Android.App.Application.Context);
#endif
        this.ShowStatus(SyncStatus.Idle);
        await this.SyncNowAsync();
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        await this.syncLauncher.RunNowAsync();
        this.ShowStatus(this.syncLauncher.Status);
    }

    [RelayCommand]
    private async Task SignOutGoogleAsync()
    {
        await this.auth.SignOutAsync();
#if ANDROID
        Platform.SyncWorker.Cancel(global::Android.App.Application.Context);
#endif
        // Записи остаются; очередь и GoogleEventId прежнего аккаунта сохраняются как есть (решение плана).
        this.syncStore.Save(this.syncStore.Load() with { Email = null, SyncToken = null, CalendarId = null });
        this.cache.Remove("google-access-token");
        this.syncLauncher.Reset();
        this.ShowStatus(SyncStatus.Idle);
    }
}
