using Android.Content;
using Android.Runtime;
using AndroidX.Health.Connect.Client;
using AndroidX.Health.Connect.Client.Records;
using AndroidX.Health.Connect.Client.Records.Metadata;
using AndroidX.Health.Connect.Client.Request;
using AndroidX.Health.Connect.Client.Response;
using AndroidX.Health.Connect.Client.Time;
using Chrono.Services;
using Java.Time;
using Kotlin.Coroutines;
using Kotlin.Jvm;

namespace Chrono.Platform;

/// <summary>
/// Чтение данных часов из Health Connect: туда пишут Pixel Watch, Galaxy Watch (Samsung Health),
/// Garmin, Mi Band (Zepp) и др. Стадии сна часы пишут обычно после пробуждения, пульс — пачками,
/// поэтому свежесть данных проверяет SleepPhaseEstimator.
/// </summary>
public static class HealthConnect
{
    public const string ReadHeartRate = "android.permission.health.READ_HEART_RATE";
    public const string ReadSleep = "android.permission.health.READ_SLEEP";
    public const string ReadInBackground = "android.permission.health.READ_HEALTH_DATA_IN_BACKGROUND";

    /// <summary>Все разрешения умного пробуждения; чтение в фоне нужно, потому что окно идёт при выключенном экране.</summary>
    public static readonly string[] Permissions = [ReadHeartRate, ReadSleep, ReadInBackground];

    public const string ProviderPackage = "com.google.android.apps.healthdata";

    public static bool IsAvailable(Context context) =>
        HealthConnectClient.GetSdkStatus(context) == HealthConnectClient.SdkAvailable;

    public static async Task<IReadOnlyCollection<string>> GetGrantedAsync(Context context)
    {
        var client = HealthConnectClient.GetOrCreate(context);
        var result = await CallSuspendAsync(continuation => client.PermissionController.GetGrantedPermissions(continuation));
        var granted = result.JavaCast<Java.Util.ISet>()!;
        return Permissions.Where(p => granted.Contains(new Java.Lang.String(p))).ToList();
    }

    public static async Task<IReadOnlyList<HeartRateSample>> ReadHeartRateAsync(Context context, DateTimeOffset from, DateTimeOffset to)
    {
        var client = HealthConnectClient.GetOrCreate(context);
        var request = new ReadRecordsRequest(
            JvmClassMappingKt.GetKotlinClass(Java.Lang.Class.FromType(typeof(HeartRateRecord))),
            TimeRangeFilter.Between(Instant.OfEpochMilli(from.ToUnixTimeMilliseconds())!, Instant.OfEpochMilli(to.ToUnixTimeMilliseconds())!),
            new List<DataOrigin>(),
            true,
            1000,
            null!);
        var response = (await CallSuspendAsync(continuation => client.ReadRecords(request, continuation))).JavaCast<ReadRecordsResponse>()!;

        var samples = new List<HeartRateSample>();
        foreach (var item in response.Records)
        {
            foreach (var sample in ((Java.Lang.Object)item!).JavaCast<HeartRateRecord>()!.Samples)
            {
                samples.Add(new HeartRateSample(DateTimeOffset.FromUnixTimeMilliseconds(sample.Time.ToEpochMilli()), sample.BeatsPerMinute));
            }
        }

        return samples;
    }

    /// <summary>Стадия из записи сна, покрывающей текущий момент (обычно её ещё нет — часы пишут сон после пробуждения).</summary>
    public static async Task<WatchSleepStage> ReadCurrentStageAsync(Context context, DateTimeOffset now)
    {
        var client = HealthConnectClient.GetOrCreate(context);
        var request = new ReadRecordsRequest(
            JvmClassMappingKt.GetKotlinClass(Java.Lang.Class.FromType(typeof(SleepSessionRecord))),
            TimeRangeFilter.Between(Instant.OfEpochMilli(now.AddHours(-16).ToUnixTimeMilliseconds())!, Instant.OfEpochMilli(now.AddMinutes(1).ToUnixTimeMilliseconds())!),
            new List<DataOrigin>(),
            false,
            20,
            null!);
        var response = (await CallSuspendAsync(continuation => client.ReadRecords(request, continuation))).JavaCast<ReadRecordsResponse>()!;

        var nowMs = now.ToUnixTimeMilliseconds();
        foreach (var item in response.Records)
        {
            foreach (var stage in ((Java.Lang.Object)item!).JavaCast<SleepSessionRecord>()!.Stages)
            {
                if (stage.StartTime.ToEpochMilli() > nowMs || stage.EndTime.ToEpochMilli() <= nowMs)
                {
                    continue;
                }

                var type = stage.GetStage();
                return type == SleepSessionRecord.StageTypeDeep ? WatchSleepStage.Deep
                    : type == SleepSessionRecord.StageTypeLight ? WatchSleepStage.Light
                    : type == SleepSessionRecord.StageTypeRem ? WatchSleepStage.Rem
                    : type == SleepSessionRecord.StageTypeAwake || type == SleepSessionRecord.StageTypeAwakeInBed ? WatchSleepStage.Awake
                    : WatchSleepStage.None;
            }
        }

        return WatchSleepStage.None;
    }

    /// <summary>
    /// Вызов Kotlin suspend-функции: биндинг принимает IContinuation. Если функция не приостановилась,
    /// результат возвращается сразу; иначе приходит в ResumeWith (ошибка — как kotlin.Result$Failure).
    /// Приём взят из Shiny.Health (shinyorg/health, HealthService.CallSuspendAsync).
    /// </summary>
    private static Task<Java.Lang.Object> CallSuspendAsync(Func<IContinuation, Java.Lang.Object?> call)
    {
        var tcs = new TaskCompletionSource<Java.Lang.Object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var immediate = call(new SuspendContinuation(tcs));
        if (immediate is not null && immediate.Class?.Name != "kotlin.coroutines.intrinsics.CoroutineSingletons")
        {
            SuspendContinuation.Complete(tcs, immediate);
        }

        return tcs.Task;
    }

    private sealed class SuspendContinuation : Java.Lang.Object, IContinuation
    {
        private readonly TaskCompletionSource<Java.Lang.Object> tcs;

        public SuspendContinuation(TaskCompletionSource<Java.Lang.Object> tcs)
        {
            this.tcs = tcs;
        }

        public ICoroutineContext Context => EmptyCoroutineContext.Instance;

        public void ResumeWith(Java.Lang.Object result) => Complete(this.tcs, result);

        public static void Complete(TaskCompletionSource<Java.Lang.Object> tcs, Java.Lang.Object result)
        {
            if (result.Class?.Name != "kotlin.Result$Failure")
            {
                tcs.TrySetResult(result);
                return;
            }

            // Неуспех Kotlin Result приходит обёрткой с полем exception — достаём настоящую ошибку Health Connect.
            var fieldId = JNIEnv.GetFieldID(result.Class.Handle, "exception", "Ljava/lang/Throwable;");
            var handle = fieldId == IntPtr.Zero ? IntPtr.Zero : JNIEnv.GetObjectField(result.Handle, fieldId);
            Exception error = handle == IntPtr.Zero
                ? new InvalidOperationException("Health Connect вернул ошибку без описания.")
                : GetObject<Java.Lang.Throwable>(handle, JniHandleOwnership.TransferLocalRef)!;
            tcs.TrySetException(error);
        }
    }
}
