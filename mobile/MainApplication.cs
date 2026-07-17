using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace FlexFamilyCalendar.Android;

/// <summary>
/// Registriert die shared <see cref="FlexFamilyCalendar.App"/> als Avalonia-Application für
/// Android und signalisiert der Weiche in App.OnFrameworkInitializationCompleted, dass wir
/// im Android-SingleView-Kontext laufen.
/// </summary>
[Application]
public class MainApplication : AvaloniaAndroidApplication<FlexFamilyCalendar.App>
{
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        FlexFamilyCalendar.App.IsAndroid = true;
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
