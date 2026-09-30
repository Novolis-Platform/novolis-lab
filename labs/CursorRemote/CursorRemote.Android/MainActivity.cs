using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia.Android;

namespace CursorRemote.Android;

[Activity(
    Label = "Cursor Remote",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode,
    Exported = true)]
public sealed class MainActivity : AvaloniaMainActivity;
