using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace FlexFamilyCalendar.Android;

[Activity(
    Label = "FlexFamily Calendar",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Signalisiert der shared App, dass wir im Android-SingleView-Kontext laufen
        // (unterscheidet sich vom Browser-Head: hat Dateisystem, kein localStorage-Interop).
        FlexFamilyCalendar.App.IsAndroid = true;
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
