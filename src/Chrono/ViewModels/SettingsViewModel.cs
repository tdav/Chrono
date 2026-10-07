using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISmartWake smartWake;
    private readonly AlarmService alarmService;

    public SettingsViewModel(ISmartWake smartWake, AlarmService alarmService)
    {
        this.smartWake = smartWake;
        this.alarmService = alarmService;
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

    public async Task RefreshAsync()
    {
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
    private Task CloseAsync() => Shell.Current.GoToAsync("..");
}
