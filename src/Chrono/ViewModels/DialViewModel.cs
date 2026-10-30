using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Строка сводки без выбора: текст и дело, в редактор которого она ведёт (null — «Свободно весь день»).</summary>
public sealed record DialIdleLine(Guid? Id, string Text);

/// <summary>Вкладка «Циферблат»: сегодняшний день секторами, выбор записи, карточка с действиями («Изменить», «Сделано», «Снять выбор»), «+».</summary>
public sealed partial class DialViewModel : ObservableObject
{
    private readonly AlarmService alarmService;
    private readonly TimeProvider timeProvider;
    private IReadOnlyList<DayEntry> entries = [];
    private DayEntry? selected;
    private DateTime now;
    // Половина суток, в которой было «сейчас» при прошлом пересчёте: при первом показе и после полудня или полуночи
    // сегмент переключается на текущую половину — на ней стрелка «сейчас».
    private DialHalf? lastNowHalf;

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
    [NotifyPropertyChangedFor(nameof(HalfIndex))]
    public partial bool IsEvening { get; set; }

    /// <summary>Сегмент «День / Вечер» (SegmentedControl): 0 — 00:00–12:00, 1 — 12:00–24:00. Переключение снимает выбор: записи может не быть на другой половине.</summary>
    public int HalfIndex
    {
        get => this.IsEvening ? 1 : 0;
        set
        {
            if (value == this.HalfIndex)
            {
                return;
            }

            this.IsEvening = value == 1;
            this.selected = null;
            this.Refresh();
        }
    }

    /// <summary>Подстрока шапки — сегодняшняя дата: «Среда, 21 октября».</summary>
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

    /// <summary>Категория выбранной записи 0–5: точка в карточке красится ролью Cat1…Cat6 и следует теме.</summary>
    [ObservableProperty]
    public partial int SelectedCategory { get; set; }

    /// <summary>Кнопка карточки: «Сделано» или, если запись уже отмечена, «Снять отметку».</summary>
    [ObservableProperty]
    public partial string SelectedDoneText { get; set; } = "";

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
        if (nowHalf != this.lastNowHalf)
        {
            this.lastNowHalf = nowHalf;
            this.IsEvening = nowHalf == DialHalf.Evening;
        }

        // «День» — всегда 00:00–12:00, «Вечер» — 12:00–24:00 (решение пользователя).
        var half = this.IsEvening ? DialHalf.Evening : DialHalf.Morning;
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
        // В ru платформа отдаёт день недели строчными: первая буква заглавная.
        var date = this.now.ToString("dddd, d MMMM", culture);
        this.TitleDate = culture.TextInfo.ToUpper(date[0]) + date[1..];

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
            this.SelectedCategory = DialLayout.ColorIndex(sel.Alarm.Id);
            this.SelectedDoneText = sel.IsDone ? AppResources.DialUnmarkDone : AppResources.DialMarkDone;
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

    /// <summary>
    /// Касание циферблата: угол от «12 часов» по часовой и доля радиуса обода. Сектор выделяется; касание пустого места, центра и места
    /// за ободом ничего не делает (DESIGN.md §4.10) — выбор снимает кнопка «Снять выбор» или переключение сегмента.
    /// </summary>
    public void SelectAt(double angle, double radiusFraction)
    {
        if (DialLayout.HitTest(this.Slices, angle, radiusFraction, this.SelectedId) is not { } hit)
        {
            return;
        }

        this.selected = hit;
        this.Refresh();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        this.selected = null;
        this.Refresh();
    }

    // Отметка «сделано» ставится на день выбранной записи; выбор сохраняется, кнопка меняет подпись.
    [RelayCommand]
    private void ToggleDone()
    {
        if (this.selected is not { } entry)
        {
            return;
        }

        this.alarmService.SetDone(entry.Alarm.Id, DateOnly.FromDateTime(entry.Start), !entry.IsDone);
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
