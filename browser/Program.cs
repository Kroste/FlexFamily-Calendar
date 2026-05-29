using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using FlexFamilyCalendar;
using FlexFamilyCalendar.Browser;

[assembly: SupportedOSPlatform("browser")]

internal sealed partial class Program
{
    public static async Task Main(string[] args)
    {
        // localStorage-Adapter dem App-Statics geben, bevor die App startet.
        App.BrowserStore = new BrowserLocalStorage();
        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .WithInterFont();
}
