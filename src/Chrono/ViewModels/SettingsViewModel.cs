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
    private readonly IReliabilityChecks reliability;

    public SettingsViewModel(
        ISmartWake smartWake,
        AlarmService alarmService,
        IGoogleAuth auth,
        GoogleCalendarClient google,
        SyncStore syncStore,
        SyncLauncher syncLauncher,
        IMemoryCache cache,
        IReliabilityChecks reliability)
    {
        this.smartWake = smartWake;
        this.alarmService = alarmService;
        this.auth = auth;
        this.google = google;
        this.syncStore = syncStore;
        this.syncLauncher = syncLauncher;
        this.cache = cache;
        this.reliability = reliability;
        this.smartWakeEnabled = smartWake.IsSupported && SmartWakeSettings.Enabled;
        this.mathChallengeEnabled = DismissSettings.MathChallenge;
        this.theme = ThemeSettings.Theme;
    }

    public bool IsSmartWakeSupported => this.smartWake.IsSupported;

    // Поле, а не partial-свойство: начальное значение задаётся без вызова OnSmartWakeEnabledChanged.
    [ObservableProperty]
    private bool smartWakeEnabled;

    // Поле по той же причине: начальное значение не должно вызывать OnMathChallengeEnabledChanged.
    [ObservableProperty]
    private bool mathChallengeEnabled;

    // Поле по той же причине: начальное значение не должно вызывать OnThemeChanged. Unspecified — «Система».
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThemeSystem))]
    [NotifyPropertyChangedFor(nameof(IsThemeLight))]
    [NotifyPropertyChangedFor(nameof(IsThemeDark))]
    private AppTheme theme;

    public bool IsThemeSystem => this.Theme == AppTheme.Unspecified;

    public bool IsThemeLight => this.Theme == AppTheme.Light;

    public bool IsThemeDark => this.Theme == AppTheme.Dark;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccessText))]
    public partial string AccessText { get; set; } = "";

    // Пока доступ не проверен, строка статуса данных часов скрыта: иначе мелькал бы значок без текста.
    public bool HasAccessText => this.AccessText.Length > 0;

    /// <summary>Доступ к данным часов есть: статус показывается значком ✓ (иначе «!») и словами в <see cref="AccessText"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessNeedsAttention))]
    public partial bool AccessGranted { get; set; }

    public bool AccessNeedsAttention => !this.AccessGranted;

    [ObservableProperty]
    public partial bool CanGrantAccess { get; set; }

    /// <summary>Число пунктов «Надёжности», требующих исправления: красный бейдж в строке «Надёжность».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReliabilityIssues))]
    [NotifyPropertyChangedFor(nameof(NoReliabilityIssues))]
    [NotifyPropertyChangedFor(nameof(ReliabilityDescription))]
    public partial int ReliabilityIssues { get; set; }

    public bool HasReliabilityIssues => this.ReliabilityIssues > 0;

    public bool NoReliabilityIssues => this.ReliabilityIssues == 0;

    // Бейдж — одно число, и экранный диктор прочёл бы просто «1»: описание строки называет раздел и смысл числа.
    public string ReliabilityDescription => this.HasReliabilityIssues
        ? $"{AppResources.ReliabilityTitle}, {string.Format(AppResources.ReliabilityIssues, this.ReliabilityIssues)}"
        : AppResources.ReliabilityTitle;

    /// <summary>Сноска внизу экрана: версия приложения.</summary>
    public string VersionText => string.Format(AppResources.VersionFooter, AppInfo.Current.VersionString);

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

    /// <summary>
    /// Вход выполнен, но доступ отозван: нужна кнопка входа без потери очереди (выход её очистил бы).
    /// Зависит от статуса лаунчера, а не от последнего показанного текста: неудачный вход кнопку не прячет.
    /// </summary>
    public bool NeedsSignInAgain => this.GoogleEmail is not null && this.syncLauncher.Status == SyncStatus.SignInRequired;

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
        // Ошибки видны и без входа: иначе неудачная попытка войти ничего бы не показала.
        this.SyncStatusText = status switch
        {
            SyncStatus.NoNetwork => AppResources.SyncNoNetwork,
            SyncStatus.SignInRequired => AppResources.SyncSignInAgain,
            SyncStatus.Failed => AppResources.SyncFailed,
            _ when state.Email is not null && state.LastSyncUtc is { } last =>
                string.Format(AppResources.SyncedAt, DateTime.SpecifyKind(last, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm")),
            _ => "",
        };
        this.OnPropertyChanged(nameof(this.NeedsSignInAgain));
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
        this.AccessGranted = access == SmartWakeAccess.Granted;
        this.CanGrantAccess = this.SmartWakeEnabled && access is SmartWakeAccess.NoPermission or SmartWakeAccess.HealthConnectMissing;

        // Возврат с экрана «Надёжность» снова приводит сюда (OnAppearing), поэтому бейдж отражает исправленное.
        this.ReliabilityIssues = (await this.reliability.GetStatusAsync()).Count(status => !status.Ok);
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

    // Выбор сохраняется и применяется сразу: ThemeService перекладывает цвета по RequestedThemeChanged.
    partial void OnThemeChanged(AppTheme value)
    {
        ThemeSettings.Theme = value;
        Application.Current!.UserAppTheme = value;
    }

    // Параметр — имя AppTheme из разметки: «Unspecified», «Light» или «Dark».
    [RelayCommand]
    private void SetTheme(string theme) => this.Theme = Enum.Parse<AppTheme>(theme);

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

            // Тот же аккаунт после «Нужно войти снова» сохраняет очередь; другой аккаунт очищается как при выходе.
            await this.syncLauncher.CompleteSignInAsync(
                email,
                this.alarmService,
#if ANDROID
                () => Platform.SyncWorker.Cancel(global::Android.App.Application.Context),
#else
                () => { },
#endif
                this.cache);
        }
        catch (SyncScopesMissingException)
        {
            // В окне согласия снята галочка с области календаря: показываем причину, а не общую «Ошибку синхронизации».
            this.ShowStatus(SyncStatus.Idle);
            this.SyncStatusText = AppResources.SyncScopesMissing;
            return;
        }
        catch (Exception ex)
        {
            this.ShowStatus(SyncLauncher.StatusOf(ex));
            return;
        }

        try
        {
#if ANDROID
            Platform.SyncWorker.Schedule(global::Android.App.Application.Context);
#endif
        }
        catch (Exception ex)
        {
            // Исключение команды (async void) уронило бы приложение.
            this.ShowStatus(SyncLauncher.StatusOf(ex));
            return;
        }

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
        try
        {
            // Без сети отзыв доступа не проходит и бросает: выход можно повторить, ничего не очищаем.
            await this.auth.SignOutAsync();
        }
        catch (Exception ex)
        {
            this.ShowStatus(SyncLauncher.StatusOf(ex));
            return;
        }

        try
        {
            await this.syncLauncher.SignOutAsync(
                this.alarmService,
#if ANDROID
                () => Platform.SyncWorker.Cancel(global::Android.App.Application.Context),
#else
                () => { },
#endif
                this.cache);
        }
        catch (Exception ex)
        {
            // Исключение команды (async void) уронило бы приложение.
            this.ShowStatus(SyncLauncher.StatusOf(ex));
            return;
        }

        this.ShowStatus(SyncStatus.Idle);
    }
}
