using Android.App;
using Android.Runtime;
namespace OBS.Maui;
[Application]
public sealed class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
