using System.Runtime.InteropServices.JavaScript;

namespace FlexFamilyCalendar.Browser;

/// <summary>JS-Interop für window.location-Werte, die der .NET-Head zum Aufbau absoluter URLs braucht.</summary>
public partial class BrowserLocation
{
    [JSImport("globalThis.flexFamilyGetOrigin")]
    public static partial string GetOrigin();
}
