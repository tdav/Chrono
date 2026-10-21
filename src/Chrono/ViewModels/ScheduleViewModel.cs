using System.Collections.ObjectModel;
using System.Globalization;
using Chrono.Models;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Ячейка недели. Запись, чтобы ленту можно было сравнивать по значению и не пересобирать без нужды.</summary>
public sealed record DayCell(DateOnly Date, int DotCount, bool IsSelected, bool IsToday)
{
    /// <summary>Сокращение дня недели с заглавной буквы (в ru платформа отдаёт строчные).</summary>
    public string Label
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture;
            var name = culture.DateTimeFormat.AbbreviatedDayNames[(int)this.Date.DayOfWeek];
            return culture.TextInfo.ToUpper(name[0]) + name[1..];
        }
    }

    /// <summary>Колонка в сетке недели: понедельник — 0, воскресенье — 6.</summary>
    public int Column => ((int)this.Date.DayOfWeek + 6) % 7;

    public string Number => this.Date.Day.ToString(CultureInfo.CurrentUICulture);

    /// <summary>Для озвучивания TalkBack: «понедельник, 20 октября».</summary>
    public string AccessibleText => this.Date.ToString("dddd, d MMMM", CultureInfo.CurrentUICulture);

    /// <summary>Золотая подпись выходного; у выбранного дня текст светлый на металле.</summary>
    public bool IsWeekendInk => this.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && !this.IsSelected;

    public IReadOnlyList<int> Dots => Enumerable.Range(0, this.DotCount).ToList();
}

/// <summary>Строка ленты дня: запись и готовые к показу тексты. RangeText входит в равенство — «осталось N мин» меняется со временем.</summary>
public sealed record DayEntryItem(DayEntry Entry, string RangeText)
{
    public string TimeText => this.Entry.Start.ToString("HH:mm");

    public string Title => string.IsNullOrWhiteSpace(this.Entry.Alarm.Label) ? AppResources.DefaultLabel : this.Entry.Alarm.Label;

    public string IconImage => ItemIcons.ImageFor(this.Entry.Alarm.Icon);

    public bool IsCurrent => this.Entry.IsCurrent;

    public bool IsDone => this.Entry.IsDone;

    /// <summary>Без длительности строки диапазона нет: время уже стоит в колонке слева.</summary>
    public bool HasRange => this.RangeText.Length > 0;

    /// <summary>Подпись кружка для TalkBack зависит от состояния: у отмеченного — «Снять отметку».</summary>
    public string DoneDescription => this.Entry.IsDone ? AppResources.UnmarkDone : AppResources.MarkDone;

    public bool IsMuted => this.Entry.IsMuted;

    /// <summary>Текущее дело — вертикальная капсула: 1,2 pt на минуту, но не ниже круга 42.</summary>
    public double CapsuleHeight => this.Entry.IsCurrent ? Math.Max(42, this.Entry.Alarm.DurationMinutes * 1.2) : 42;
}

public sealed partial class ScheduleViewModel : ObservableObject, IQueryAttributable
{
    private readonly AlarmService alarmService;
    private readonly TimeProvider timeProvider;

    public ScheduleViewModel(AlarmService alarmService, TimeProvider timeProvider)
    {
        this.alarmService = alarmService;
        this.timeProvider = timeProvider;
    }

    public ObservableCollection<DayEntryItem> Entries { get; } = [];

    public ObservableCollection<DayCell> Days { get; } = [];

    // Начальное значение — инициализатором, а не присваиванием в конструкторе: так OnSelectedDayChanged (и Refresh) не вызывается.
    // Данные загрузит первый OnAppearing страницы.
    [ObservableProperty]
    public partial DateOnly SelectedDay { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    public partial string TitleDay { get; set; } = "";

    [ObservableProperty]
    public partial string TitleYear { get; set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool IsAddEnabled { get; set; }

    partial void OnSelectedDayChanged(DateOnly value) => this.Refresh();

    /// <summary>Параметр day=yyyy-MM-dd приходит от ScheduleLauncher (нажатие на уведомление).</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("day", out var value)
            && DateOnly.TryParseExact(value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            this.SelectedDay = day;
        }
    }

    /// <summary>Перечитывает записи и отметки. Вызывается из OnAppearing, таймера страницы и при смене дня.</summary>
    public void Refresh()
    {
        var now = this.timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var alarms = this.alarmService.GetAll();
        var done = this.alarmService.GetDone();
        var selected = this.SelectedDay;

        // Пробел между днём и годом — часть значения: хвостовой пробел в StringFormat внутри Span XAML обрезает.
        this.TitleDay = selected.ToString("d MMMM", CultureInfo.CurrentUICulture) + " ";
        this.TitleYear = selected.Year.ToString(CultureInfo.CurrentUICulture);
        this.IsAddEnabled = selected >= today;

        var days = DayPlan.Week(selected)
            .Select(d => new DayCell(d, Math.Min(DayPlan.For(alarms, done, d, now).Count, 5), d == selected, d == today))
            .ToList();

        var items = DayPlan.For(alarms, done, selected, now)
            .Select(e =>
            {
                if (e.Alarm.DurationMinutes <= 0)
                {
                    return new DayEntryItem(e, string.Empty);
                }

                var range = $"{e.Start:HH:mm}–{e.End:HH:mm} · ";
                range += e.IsCurrent && AlarmSchedule.Until(e.End, now) is { } left
                    ? string.Format(AppResources.ScheduleLeft, left.Days * 1440 + left.Hours * 60 + left.Minutes)
                    : string.Format(AppResources.DurationMinutes, e.Alarm.DurationMinutes);
                return new DayEntryItem(e, range);
            })
            .ToList();

        // Обновляем на месте: Clear + Add сбрасывает прокрутку CollectionView к началу, а «осталось N мин» меняется каждую минуту.
        // Совпадающие позиции не трогаем, различающиеся заменяем, лишние удаляем, недостающие добавляем.
        for (var i = 0; i < days.Count; i++)
        {
            if (i >= this.Days.Count)
            {
                this.Days.Add(days[i]);
            }
            else if (this.Days[i] != days[i])
            {
                this.Days[i] = days[i];
            }
        }

        while (this.Days.Count > days.Count)
        {
            this.Days.RemoveAt(this.Days.Count - 1);
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (i >= this.Entries.Count)
            {
                this.Entries.Add(items[i]);
            }
            else if (this.Entries[i] != items[i])
            {
                this.Entries[i] = items[i];
            }
        }

        while (this.Entries.Count > items.Count)
        {
            this.Entries.RemoveAt(this.Entries.Count - 1);
        }

        this.IsEmpty = items.Count == 0;
    }

    [RelayCommand]
    private void SelectDay(DateOnly day) => this.SelectedDay = day;

    [RelayCommand]
    private void NextWeek() => this.SelectedDay = this.SelectedDay.AddDays(7);

    [RelayCommand]
    private void PrevWeek() => this.SelectedDay = this.SelectedDay.AddDays(-7);

    [RelayCommand]
    private void ToggleDone(DayEntryItem item)
    {
        this.alarmService.SetDone(item.Entry.Alarm.Id, DateOnly.FromDateTime(item.Entry.Start), !item.IsDone);
        this.Refresh();
    }

    [RelayCommand]
    private Task EditAsync(DayEntryItem item) => Shell.Current.GoToAsync($"edit?id={item.Entry.Alarm.Id}");

    // Спецификация 6.5: на прошедший день запись не добавляют.
    [RelayCommand(CanExecute = nameof(IsAddEnabled))]
    private Task AddAsync() =>
        Shell.Current.GoToAsync($"edit?day={this.SelectedDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&alert=Notify");
}
