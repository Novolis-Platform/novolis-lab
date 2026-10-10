using Android.App;
using Android.Content.PM;

namespace RaylibHello.Android;

/// <summary>
/// NativeActivity host for <c>libnovolis_raylib_android.so</c> (static-linked raylib).
/// </summary>
[Activity(
    Label = "Raylib Hello",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden,
    LaunchMode = LaunchMode.SingleTask,
    AlwaysRetainTaskState = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape)]
[MetaData("android.app.lib_name", Value = "novolis_raylib_android")]
public class MainActivity : NativeActivity
{
}
