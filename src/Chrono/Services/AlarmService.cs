using Chrono.Models;

namespace Chrono.Services;

/// <summary>API будильников для UI и receiver'ов: store + синхронизация с планировщиком ОС.</summary>
public sealed class AlarmService
{
    private readonly AlarmStore store;
    private readonly IAlarmScheduler scheduler;
    private readonly TimeProvider time;
    private readonly SyncStore? sync;

    /// <param name="sync">Очередь изменений для Google; null — экземпляры без синхронизации (receiver'ы, SmartWake).</param>
    public AlarmService(AlarmStore store, IAlarmScheduler scheduler, TimeProvider time, SyncStore? sync = null)
    {
        this.store = store;
        this.scheduler = scheduler;
        this.time = time;
        this.sync = sync;
    }

    /// <summary>Запись изменена пользователем (Save/Toggle/Delete без fromSync): повод запустить синхронизацию.</summary>
    public event EventHandler? Changed;

    /// <summary>true, если при последнем чтении файл оказался повреждён и был отложен в сторону.</summary>
    public bool RecoveredFromCorruptFile { get; private set; }

    /// <summary>Последняя ошибка планировщика ОС (null — ошибок не было). Будильник при этом сохранён.</summary>
    public Exception? SchedulingError { get; private set; }

    /// <summary>
    /// Все будильники по времени. Для каждой записи по порядку:
    /// 1) нет StartsOn (старый файл) — миграция: у разовой это день At, у повторяющейся не позже сегодняшнего дня;
    /// 2) «Один раз и удалить» с истёкшим ExpiresAt — удаляется, включена она или нет;
    /// 3) сработавшая (включённая с At ≤ now): разовая выключается, «Один раз и удалить» остаётся как есть,
    /// повторяющаяся переходит на следующее срабатывание.
    /// Изменения сохраняются.
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
                // Миграция старых записей: у разовой днём начала считаем день срабатывания. У повторяющейся At мог
                // уже уйти на завтра, а сегодня она срабатывала, поэтому не позже сегодняшнего дня.
                var atDay = DateOnly.FromDateTime(alarms[i].At);
                var today = DateOnly.FromDateTime(now);
                alarms[i] = alarms[i] with { StartsOn = alarms[i].IsOneShot() || atDay < today ? atDay : today };
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

    /// <summary>
    /// Создать или заменить будильник. false — время уже прошло, ничего не сохранено.
    /// fromSync = true — запись пришла из Google: в очередь не ставится, Changed не вызывается.
    /// </summary>
    public bool Save(Alarm alarm, bool fromSync = false)
    {
        // «Настенное» время храним без пояса: Kind=Local сериализуется со смещением,
        // и после смены часового пояса 06:30 превратилось бы в другой час.
        alarm = alarm with { At = DateTime.SpecifyKind(alarm.At, DateTimeKind.Unspecified) };
        alarm = alarm with { StartsOn = alarm.StartsOn ?? DateOnly.FromDateTime(alarm.At) };
        var now = this.time.GetLocalNow().DateTime;
        if (!alarm.IsOneShot() && alarm.At < alarm.StartsOn!.Value.ToDateTime(TimeOnly.MinValue))
        {
            // Планировщик Android берёт At как есть: без нормализации запись сработала бы раньше даты начала.
            alarm = alarm with { At = alarm.NextAfter(now) };
        }

        if (alarm.At <= now)
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

        if (!fromSync)
        {
            // Очередь пишем после планировщика: её сбой не должен мешать звонку.
            this.sync?.Enqueue(new PendingChange(alarm.Id, SyncOp.Upsert, alarm.GoogleEventId, this.time.GetUtcNow().UtcDateTime));
            this.Changed?.Invoke(this, EventArgs.Empty);
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

        this.sync?.Enqueue(new PendingChange(id, SyncOp.Upsert, alarms[index].GoogleEventId, this.time.GetUtcNow().UtcDateTime));
        this.Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Удалить. Без fromSync: у чужой серии id события попадает в IgnoredEventIds (чтобы не загрузить снова),
    /// у отправленной в Google записи в очередь ставится Delete. fromSync = true — только локальное удаление.
    /// </summary>
    public void Delete(Guid id, bool fromSync = false)
    {
        var all = this.GetAll();
        var removed = all.FirstOrDefault(a => a.Id == id);
        var alarms = all.Where(a => a.Id != id).ToList();
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

        if (fromSync || removed is null)
        {
            return;
        }

        if (this.sync is not null && removed.GoogleEventId is { } eventId)
        {
            if (removed.ForeignSeries)
            {
                var state = this.sync.Load();
                if (!state.IgnoredEventIds.Contains(eventId))
                {
                    state.IgnoredEventIds.Add(eventId);
                    this.sync.Save(state);
                }
            }
            else
            {
                this.sync.Enqueue(new PendingChange(id, SyncOp.Delete, eventId, this.time.GetUtcNow().UtcDateTime));
            }
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
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
