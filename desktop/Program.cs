using Avalonia;
using FlexFamilyCalendar.Services;
using System;
using System.Threading.Tasks;

namespace FlexFamilyCalendar;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
