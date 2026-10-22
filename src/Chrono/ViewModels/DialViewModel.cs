using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Строка сводки без выбора: текст и дело, в редактор которого она ведёт (null — «Свободно весь день»).</summary>
public sealed record DialIdleLine(Guid? Id, string Text);

/// <summary>Вкладка «Циферблат»: сегодняшний день секторами, выбор записи, карточка (нажатие — редактор), «+».</summary>
public sealed partial class DialViewModel : ObservableObject
{
    private readonly AlarmService alarmService;
    private readonly TimeProvider timeProvider;
    private IReadOnlyList<DayEntry> entries = [];
    private DayEntry? selected;
    private DateTime now;

    public DialViewModel(AlarmService alarmService, TimeProvider timeProvider)
    {
        this.alarmService = alarmService;
        this.timeProvider = timeProvider;
    }

    /// <summary>Данные для отрисовки изменились: странице нужно передать их в DialDrawable и перерисовать.</summary>
    public event EventHandler? DialChanged;

    public IReadOnlyList<DialSlice> Slices { get; private set; } = [];

    public Guid? SelectedId => this.selected?.Alarm.Id;

    /// <summary>Угол стрелки «сейчас»; null, если показана не текущая половина суток.</summary>
    public double? ArrowAngle { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNowTab))]
    public partial bool IsEvening { get; set; }

    public bool IsNowTab => !this.IsEvening;

    [ObservableProperty]
    public partial string TitleDate { get; set; } = "";

    [ObservableProperty]
    public partial string CenterTitle { get; set; } = "";

    [ObservableProperty]
    public partial string CenterSub { get; set; } = "";

    [ObservableProperty]
    public partial string DialDescription { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool HasSelection { get; set; }

    public bool IsIdle => !this.HasSelection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardDescription))]
    public partial string SelectedTitle { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardDescription))]
    public partial string SelectedDetails { get; set; } = "";

    /// <summary>Подпись карточки для TalkBack.</summary>
    public string CardDescription => $"{this.SelectedTitle}, {this.SelectedDetails}";

    [ObservableProperty]
    public partial Color SelectedColor { get; set; } = Colors.Transparent;

    /// <summary>Верхняя строка сводки: идущее дело, иначе ближайшее, иначе «Свободно весь день» (Id = null).</summary>
    [ObservableProperty]
    public partial DialIdleLine IdleHeadline { get; set; } = new(null, "");

    [ObservableProperty]
    public partial IReadOnlyList<DialIdleLine> IdleLines { get; set; } = [];

    /// <summary>Перечитывает записи дня и пересчитывает всё показываемое. Вызывается из OnAppearing, таймера и при возврате из фона.</summary>
    public void Refresh()
    {
        this.now = this.timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(this.now);
        this.entries = DayPlan.For(this.alarmService.GetAll(), this.alarmService.GetDone(), today, this.now);

        var nowHalf = DialLayout.HalfOf(this.now);
        var half = this.IsEvening ? DialHalf.Evening : nowHalf;
        this.Slices = DialLayout.Slices(this.entries, today, half);
        this.ArrowAngle = half == nowHalf ? DialLayout.AngleOf(TimeOnly.FromDateTime(this.now)) : null;

        // Выбор сохраняется, пока запись видна на показанной половине; иначе снимается вместе с раскрытой карточкой
        // (в полдень таймер сам переключает половину — утреннее дело уходит с экрана).
        var selectedId = this.SelectedId;
        this.selected = null;
        foreach (var slice in this.Slices)
        {
            if (slice.Entry.Alarm.Id == selectedId)
            {
                this.selected = slice.Entry;
            }
        }

        var culture = CultureInfo.CurrentUICulture;
        this.TitleDate = this.now.ToString("ddd, d MMM", culture);

        var current = DialLayout.Current(this.entries, this.now);
        var next = DialLayout.Next(this.entries, this.now);
        string? minutes = null;
        if (current is { } running && AlarmSchedule.Until(running.End, this.now) is { } until)
        {
            minutes = string.Format(culture, AppResources.DurationMinutes, until.Days * 1440 + until.Hours * 60 + until.Minutes);
        }

        var left = minutes is null ? null : string.Format(culture, AppResources.DialLeft, minutes);

        this.HasSelection = this.selected is not null;
        if (this.selected is { } sel)
        {
            var hasRange = sel.Alarm.DurationMinutes > 0;
            var range = hasRange ? $"{sel.Start:HH:mm}–{sel.End:HH:mm}" : $"{sel.Start:HH:mm}";
            var details = new List<string> { range };
            if (hasRange)
            {
                details.Add(string.Format(culture, AppResources.DurationMinutes, sel.Alarm.DurationMinutes));
            }

            details.Add(sel.Alarm.Alert switch
            {
                AlertKind.None => AppResources.Alert_None,
                AlertKind.Notify => AppResources.Alert_Notify,
                AlertKind.Beep => AppResources.Alert_Beep,
                AlertKind.Ring => AppResources.Alert_Ring,
                _ => AppResources.Alert_Ring,
            });

            this.SelectedTitle = Title(sel);
            this.SelectedDetails = string.Join(" · ", details);
            this.SelectedColor = Color.FromArgb(DialLayout.Palette[DialLayout.ColorIndex(sel.Alarm.Id)]);
            this.CenterTitle = this.SelectedTitle;
            this.CenterSub = range;
        }
        else
        {
            this.CenterTitle = this.now.ToString("HH:mm", culture);
            this.CenterSub = current is { } c && left is not null ? $"{Title(c)} · {left}" : "";
        }

        if (minutes is not null)
        {
            // Идущее дело называется в строке: «Работа · до конца 26 мин · далее Обед в 13:00».
            var text = next is { } n
                ? string.Format(culture, AppResources.DialUntilEnd, Title(current!.Value), minutes, Title(n), $"{n.Start:HH:mm}")
                : $"{Title(current!.Value)} · {left}";
            this.IdleHeadline = new DialIdleLine(current!.Value.Alarm.Id, text);
        }
        else if (next is { } following)
        {
            this.IdleHeadline = new DialIdleLine(following.Alarm.Id, string.Format(culture, AppResources.DialFree, Title(following), $"{following.Start:HH:mm}"));
        }
        else
        {
            this.IdleHeadline = new DialIdleLine(null, AppResources.DialFreeAllDay);
        }

        this.IdleLines = this.entries.Where(e => e.Start > this.now).Take(3).Select(e => new DialIdleLine(e.Alarm.Id, $"{e.Start:HH:mm} {Title(e)}")).ToList();

        // Для TalkBack — дела показанной половины одним списком (дело через полдень не дублируется).
        this.DialDescription = string.Join("; ", this.Slices.Select(s => s.Entry).Select(e => e.Alarm.DurationMinutes > 0
            ? string.Format(culture, AppResources.DialSectorDescription, Title(e), $"{e.Start:HH:mm}", $"{e.End:HH:mm}")
            : $"{Title(e)}, {e.Start:HH:mm}"));

        this.DialChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Title(DayEntry entry) => string.IsNullOrWhiteSpace(entry.Alarm.Label) ? AppResources.DefaultLabel : entry.Alarm.Label;

    /// <summary>Касание циферблата: угол от «12 часов» по часовой и доля радиуса обода. Сектор выделяется; пустое место снимает выделение.</summary>
    public void SelectAt(double angle, double radiusFraction)
    {
        if (DialLayout.HitTest(this.Slices, angle, radiusFraction, this.SelectedId) is not { } hit)
        {
            if (this.selected is not null)
            {
                this.selected = null;
                this.Refresh();
            }

            return;
        }

        this.selected = hit;
        this.Refresh();
    }

    [RelayCommand]
    private void ShowNow()
    {
        // Выбранной записи может не быть на другой половине: переключение снимает выбор.
        this.IsEvening = false;
        this.selected = null;
        this.Refresh();
    }

    [RelayCommand]
    private void ShowEvening()
    {
        this.IsEvening = true;
        this.selected = null;
        this.Refresh();
    }

    [RelayCommand]
    private Task EditAsync() => this.selected is { } entry ? Shell.Current.GoToAsync($"edit?id={entry.Alarm.Id}") : Task.CompletedTask;

    [RelayCommand]
    private Task OpenEntryAsync(DialIdleLine? line) => line?.Id is { } id ? Shell.Current.GoToAsync($"edit?id={id}") : Task.CompletedTask;

    [RelayCommand]
    private Task AddAsync()
    {
        this.Refresh();
        var free = DialLayout.FreeSlot(this.entries, this.now);
        return Shell.Current.GoToAsync(string.Format(
            CultureInfo.InvariantCulture, "edit?day={0:yyyy-MM-dd}&alert=Notify&time={0:HH:mm}&duration={1}", free, DialLayout.DefaultNewMinutes));
    }
}
