using Android.App;
using Android.Content.PM;
using Android.OS;

namespace GabSynth
{
    [Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    // ADD Keyboard, KeyboardHidden, and Navigation below:
    ConfigurationChanges = ConfigChanges.ScreenSize
                         | ConfigChanges.Orientation
                         | ConfigChanges.UiMode
                         | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize
                         | ConfigChanges.Density
                         | ConfigChanges.Keyboard          // <-- CRITICAL
                         | ConfigChanges.KeyboardHidden    // <-- CRITICAL
                         | ConfigChanges.Navigation        // <-- CRITICAL
)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
