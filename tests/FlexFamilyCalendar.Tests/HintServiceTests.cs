using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

// HintService ist ein globaler statischer Schalter — die Tests laufen sequenziell in dieser
// Klasse, dank xunit-Default-Konfiguration (Klassen sind per Default nicht parallel).
public class HintServiceTests
{
    [Fact]
    public void Setter_feuert_EnabledChanged_nur_bei_echter_Aenderung()
    {
        HintService.IsEnabled = true;   // baseline
        var fired = 0;
        void Handler(object? s, EventArgs e) => fired++;
        HintService.EnabledChanged += Handler;
        try
        {
            HintService.IsEnabled = true;   // gleicher Wert → kein Event
            HintService.IsEnabled = false;  // Wechsel → 1 Event
            HintService.IsEnabled = false;  // gleicher Wert → kein Event
            HintService.IsEnabled = true;   // Wechsel → 1 Event
        }
        finally
        {
            HintService.EnabledChanged -= Handler;
        }
        Assert.Equal(2, fired);
    }
}
