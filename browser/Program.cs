using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using FlexFamilyCalendar;
using FlexFamilyCalendar.Browser;
using NLog;
using NLog.Config;
using NLog.Targets;

[assembly: SupportedOSPlatform("browser")]

internal sealed partial class Program
{
    public static async Task Main(string[] args)
    {
        // NLog im Browser nur mit Console-Target: nlog.config nutzt ${specialfolder:LocalApplicationData}
        // + createDirs=true + throwConfigExceptions=true — das wirft im WASM-Sandbox-FS und killt die
        // Mono-Runtime beim ersten Log-Aufruf (Folge: "Assert failed: .NET runtime already exited with 1").
        var nlog = new LoggingConfiguration();
        var console = new ConsoleTarget("console") { Layout = "${level:uppercase=true} | ${message}" };
        nlog.AddRule(LogLevel.Debug, LogLevel.Fatal, console);
        LogManager.Configuration = nlog;

        // localStorage-Adapter dem App-Statics geben, bevor die App startet.
        App.BrowserStore = new BrowserLocalStorage();
        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .WithInterFont();
}
