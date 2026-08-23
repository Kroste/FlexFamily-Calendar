using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using FlexFamilyCalendar.DesignApi;
using FlexFamilyCalendar.Services;
using System;
using System.Threading.Tasks;

namespace FlexFamilyCalendar;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Master-CLAUDE.md: stiller Absturz → NLog Fatal. Handler stehen ganz oben, damit auch
        // Fehler in der Avalonia-Konfigurationsphase noch geloggt werden.
        AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
            LogService.Fatal("Unbehandelte Exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += static (_, e) =>
        {
            LogService.Fatal("Unbeobachtete Task-Exception", e.Exception);
            e.SetObserved();
        };

        // Single-Instance-Guard VOR Avalonia: ein zweiter Prozess würde ein zweites Tray-Icon
        // aufhängen und im lokalen Speicher-Modus parallel in dieselben JSON-Dateien schreiben.
        // Läuft schon eine Instanz, holen wir die nach vorn und beenden uns, ohne die UI
        // überhaupt hochzufahren.
        var guard = new SingleInstanceGuard();
        if (!guard.TryClaim())
        {
            guard.NotifyPrimary();
            guard.Dispose();
            return 0;
        }

        App.PendingGuard = guard;
        App.PendingApiArgs = args;
        App.DesignApiStarter = DesignApiHost.MaybeStart;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .With(EmojiFontOptions())
            .LogToTrace();

    /// <summary>
    /// Fallback auf den Farb-Emoji-Font des Systems. Inter bringt keine Emoji-Glyphen mit;
    /// ohne diesen Fallback rendert Avalonia zum Beispiel die Länderflaggen in der
    /// Sprachauswahl als Ersatzkästchen statt als 🇩🇪 / 🇬🇧.
    ///
    /// Wichtig: <c>WithInterFont()</c> setzt die Standard-Schriftfamilie selbst über
    /// FontManagerOptions. Da wir die Options hier ersetzen, muss Inter erneut angegeben
    /// werden — sonst fällt die ganze App auf die System-Schrift zurück.
    /// </summary>
    private static FontManagerOptions EmojiFontOptions()
    {
        var emojiFont = OperatingSystem.IsWindows() ? "Segoe UI Emoji"
            : OperatingSystem.IsMacOS() ? "Apple Color Emoji"
            : "Noto Color Emoji";

        return new FontManagerOptions
        {
            DefaultFamilyName = "fonts:Inter#Inter",
            FontFallbacks = [new FontFallback { FontFamily = new FontFamily(emojiFont) }],
        };
    }
}
