using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace SpaceFleetSurveyTeam.Android;

[Activity(
    Label = "Space Fleet: Survey Team",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    Icon = "@drawable/ic_launcher",
    RoundIcon = "@drawable/ic_launcher",
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume()
    {
        Current = this;
        base.OnResume();
    }

    protected override void OnPause()
    {
        if (ReferenceEquals(Current, this))
            Current = null;
        base.OnPause();
    }
}
