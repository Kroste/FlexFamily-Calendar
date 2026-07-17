using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace FlexFamilyCalendar.Android;

[Activity(
    Label = "FlexFamily Calendar",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
