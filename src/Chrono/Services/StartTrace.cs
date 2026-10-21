using System.Diagnostics;

namespace Chrono.Services;

/// <summary>
/// Метки времени холодного старта в logcat (тег ChronoStart). Вызовы вырезаются компилятором, пока не задан символ
/// CHRONO_STARTTRACE (<c>dotnet publish -p:ChronoStartTrace=true</c>), поэтому в обычной сборке они ничего не стоят.
/// Время метки смотрится в <c>adb logcat -v time -s ChronoStart</c> рядом со строкой «Start proc» диспетчера активностей.
/// </summary>
public static class StartTrace
{
    [Conditional("CHRONO_STARTTRACE")]
    public static void Mark(string name)
    {
#if ANDROID
        global::Android.Util.Log.Info("ChronoStart", name);
#endif
    }
}
