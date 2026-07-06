using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

// Der globale Exception-Handler in desktop/Program.cs ruft LogService.Fatal. Diese Tests
// sichern, dass die Fatal-Signatur stabil bleibt und Passwörter/Tokens aus der Nachricht fliegen —
// sonst kämen Secrets aus einer Stack-Trace-Meldung ins Log.
//
// LogService.StatusUpdated ist ein globales static event; parallel laufende Tests (AI-Provider,
// UpdateService, …) feuern es ebenfalls. Deshalb sammeln wir alle Events und prüfen mit
// Contains, statt auf das "letzte" Event zu setzen.
public class LogServiceFatalTests
{
    private static List<string> Capture(Action act)
    {
        var events = new List<string>();
        void Handler(string s) { lock (events) events.Add(s); }
        LogService.StatusUpdated += Handler;
        try { act(); }
        finally { LogService.StatusUpdated -= Handler; }
        lock (events) return events.ToList();
    }

    [Fact]
    public void Fatal_ohne_Exception_feuert_StatusUpdated_mit_FATAL_Prefix()
    {
        var events = Capture(() => LogService.Fatal("Testmeldung"));
        Assert.Contains("FATAL: Testmeldung", events);
    }

    [Fact]
    public void Fatal_mit_Exception_feuert_StatusUpdated()
    {
        var events = Capture(() => LogService.Fatal("Nachricht", new InvalidOperationException("boom")));
        Assert.Contains("FATAL: Nachricht", events);
    }

    [Fact]
    public void Fatal_maskiert_Secrets_im_Message_wie_Error()
    {
        var events = Capture(() => LogService.Fatal("crash mit api_key=abc123 im payload"));
        var fatalEvents = events.FindAll(e => e.StartsWith("FATAL: "));
        Assert.NotEmpty(fatalEvents);
        Assert.All(fatalEvents, e => Assert.DoesNotContain("abc123", e));
        Assert.Contains(fatalEvents, e => e.Contains("[REDACTED]"));
    }
}
