using System.Globalization;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed partial class RingViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly IAlarmScheduler scheduler;
    private Guid alarmId;
    private int expectedAnswer;

    public RingViewModel(AlarmService alarmService, IAlarmScheduler scheduler)
    {
        this.alarmService = alarmService;
        this.scheduler = scheduler;
    }

    [ObservableProperty]
    public partial string TimeText { get; set; } = "";

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial string DateText { get; set; } = "";

    /// <summary>Пример с вариантами ответа показан вместо кнопки «Стоп» (включается в настройках).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStopVisible))]
    public partial bool IsChallengeVisible { get; set; }

    public bool IsStopVisible => !this.IsChallengeVisible;

    [ObservableProperty]
    public partial string ChallengeText { get; set; } = "";

    /// <summary>Четыре варианта ответа, один из них верный; порядок случайный.</summary>
    [ObservableProperty]
    public partial int[] Options { get; set; } = [0, 0, 0, 0];

    [ObservableProperty]
    public partial bool IsWrongAnswer { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            this.Load(id);
        }
    }

    public void Load(Guid id)
    {
        // Новый сигнал заменяет текущий: снимаем хвост цепочки уведомлений предыдущего будильника (iOS).
        // Cancel, а не StopRinging: на Android StopRinging остановил бы сервис, который уже звонит новым сигналом.
        if (this.alarmId != Guid.Empty && this.alarmId != id)
        {
            this.scheduler.Cancel(this.alarmId);
        }

        this.alarmId = id;
        this.IsWrongAnswer = false;
        this.IsChallengeVisible = DismissSettings.MathChallenge;
        if (this.IsChallengeVisible)
        {
            this.NewChallenge();
        }
        var alarm = this.alarmService.GetAll().FirstOrDefault(a => a.Id == id);
        var at = alarm?.At ?? DateTime.Now;
        this.TimeText = at.ToString("HH:mm");
        this.DateText = at.ToString("dddd, d MMMM", CultureInfo.CurrentUICulture);
        this.Label = string.IsNullOrWhiteSpace(alarm?.Label) ? AppResources.DefaultLabel : alarm.Label;
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        this.scheduler.StopRinging(this.alarmId);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ChooseAsync(int answer)
    {
        if (answer == this.expectedAnswer)
        {
            this.scheduler.StopRinging(this.alarmId);
            await Shell.Current.GoToAsync("..");
            return;
        }

        // Неверный ответ: сигнал продолжается, вместо подбора того же ответа — новый пример.
        this.IsWrongAnswer = true;
        this.NewChallenge();
    }

    /// <summary>Сложение или вычитание двух двузначных чисел; при вычитании ответ не отрицательный.</summary>
    private void NewChallenge()
    {
        var a = Random.Shared.Next(10, 100);
        var b = Random.Shared.Next(10, 100);
        if (Random.Shared.Next(2) == 0)
        {
            this.expectedAnswer = a + b;
            this.ChallengeText = $"{a} + {b} = ?";
        }
        else
        {
            (a, b) = a >= b ? (a, b) : (b, a);
            this.expectedAnswer = a - b;
            this.ChallengeText = $"{a} − {b} = ?";
        }

        // Неверные варианты — рядом с ответом (±1, ±2, ±10, ±11), чтобы не угадывались на глаз.
        int[] deltas = [-11, -10, -2, -1, 1, 2, 10, 11];
        Random.Shared.Shuffle(deltas);
        int[] options =
        [
            this.expectedAnswer,
            .. deltas.Select(d => this.expectedAnswer + d).Where(v => v >= 0).Take(3),
        ];
        Random.Shared.Shuffle(options);
        this.Options = options;
    }
}
