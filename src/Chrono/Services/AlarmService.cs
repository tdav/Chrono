using Chrono.Models;

namespace Chrono.Services;

/// <summary>API будильников для UI и receiver'ов: store + синхронизация с планировщиком ОС.</summary>
public sealed class AlarmService
{
    private readonly AlarmStore store;
    private readonly IAlarmScheduler scheduler;
    private readonly TimeProvider time;

    public AlarmService(AlarmStore store, IAlarmScheduler scheduler, TimeProvider time)
    {
        this.store = store;
        this.scheduler = scheduler;
        this.time = time;
    }

    /// <summary>true, если при последнем чтении файл оказался повреждён и был отложен в сторону.</summary>
    public bool RecoveredFromCorruptFile { get; private set; }

    /// <summary>Последняя ошибка планировщика ОС (null — ошибок не было). Будильник при этом сохранён.</summary>
    public Exception? SchedulingError { get; private set; }

    /// <summary>
    /// Все будильники по времени. Сработавшие (включённые с At ≤ now): разовые выключаются,
    /// повторяющиеся переходят на следующее срабатывание; изменения сохраняются.
    /// </summary>
    public IReadOnlyList<Alarm> GetAll()
    {
        var (alarms, wasCorrupt) = this.store.Load();
        this.RecoveredFromCorruptFile = wasCorrupt;

        var now = this.time.GetLocalNow().DateTime;
        var changed = false;
        for (var i = alarms.Count - 1; i >= 0; i--)
        {
            if (alarms[i].StartsOn is null)
            {
                // Миграция старых записей: днём начала считаем день срабатывания.
                alarms[i] = alarms[i] with { StartsOn = DateOnly.FromDateTime(alarms[i].At) };
                changed = true;
            }

            if (alarms[i].Repeat == RepeatKind.OnceThenDelete && alarms[i].ExpiresAt() <= now)
            {
                alarms.RemoveAt(i);
                changed = true;
                continue;
            }

            if (alarms[i].IsEnabled && alarms[i].At <= now)
            {
                if (alarms[i].Repeat == RepeatKind.None)
                {
                    alarms[i] = alarms[i] with { IsEnabled = false };
                    changed = true;
                }
                else if (alarms[i].Repeat != RepeatKind.OnceThenDelete)
                {
                    alarms[i] = alarms[i] with { At = alarms[i].NextAfter(now) };
                    changed = true;
                }
            }
        }

        if (changed)
        {
            this.store.Save(alarms);
        }

        return alarms.OrderBy(a => a.At).ToList();
    }

    /// <summary>Создать или заменить будильник. false — время уже прошло, ничего не сохранено.</summary>
    public bool Save(Alarm alarm)
    {
        // «Настенное» время храним без пояса: Kind=Local сериализуется со смещением,
        // и после смены часового пояса 06:30 превратилось бы в другой час.
        alarm = alarm with { At = DateTime.SpecifyKind(alarm.At, DateTimeKind.Unspecified) };
        alarm = alarm with { StartsOn = alarm.StartsOn ?? DateOnly.FromDateTime(alarm.At) };
        if (alarm.At <= this.time.GetLocalNow().DateTime)
        {
            return false;
        }

        var alarms = this.GetAll().ToList();
        var index = alarms.FindIndex(a => a.Id == alarm.Id);
        if (index >= 0)
        {
            alarms[index] = alarm;
        }
        else
        {
            alarms.Add(alarm);
        }

        this.store.Save(alarms);

        try
        {
            this.scheduler.Sync(alarms);
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            // Будильник уже сохранён; UI покажет плашку «Будильник может не сработать».
            this.SchedulingError = ex;
        }

        return true;
    }

    /// <summary>
    /// Включить или выключить. Повторяющийся при включении переходит на ближайшее срабатывание.
    /// false — включение разового отклонено: время уже прошло.
    /// </summary>
    public bool Toggle(Guid id, bool enabled)
    {
        var alarms = this.GetAll().ToList();
        var index = alarms.FindIndex(a => a.Id == id);
        if (index < 0)
        {
            return false;
        }

        var now = this.time.GetLocalNow().DateTime;
        if (enabled && alarms[index].At <= now)
        {
            if (alarms[index].IsOneShot())
            {
                return false;
            }

            alarms[index] = alarms[index] with { At = alarms[index].NextAfter(now) };
        }

        alarms[index] = alarms[index] with { IsEnabled = enabled };
        this.store.Save(alarms);

        try
        {
            this.scheduler.Sync(alarms);
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            this.SchedulingError = ex;
        }

        return true;
    }

    public void Delete(Guid id)
    {
        var alarms = this.GetAll().Where(a => a.Id != id).ToList();
        this.store.Save(alarms);

        try
        {
            this.scheduler.Cancel(id);
            this.scheduler.Sync(alarms);
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            this.SchedulingError = ex;
        }
    }

    public IReadOnlyDictionary<Guid, HashSet<DateOnly>> GetDone() => this.store.LoadDone();

    /// <summary>Отметить запись сделанной (или снять отметку) на день. Планировщик не трогает.</summary>
    public void SetDone(Guid id, DateOnly day, bool done)
    {
        var all = this.store.LoadDone();
        if (!all.TryGetValue(id, out var days))
        {
            days = all[id] = [];
        }

        if (done)
        {
            days.Add(day);
        }
        else
        {
            days.Remove(day);
        }

        this.store.SaveDone(all);
    }

    /// <summary>Переустановить всё расписание (старт приложения, перезагрузка, смена времени/пояса).</summary>
    public void RescheduleAll()
    {
        try
        {
            this.scheduler.Sync(this.GetAll());
            this.SchedulingError = null;
        }
        catch (Exception ex)
        {
            this.SchedulingError = ex;
        }
    }
}
