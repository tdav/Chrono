using Android.App;
using Android.Runtime;

namespace Chrono;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override void OnCreate()
	{
		Chrono.Services.StartTrace.Mark("Application.OnCreate: начало");
		base.OnCreate();
		Chrono.Services.StartTrace.Mark("Application.OnCreate: конец");
	}
}
