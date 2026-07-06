using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

// Der globale Exception-Handler in desktop/Program.cs ruft LogService.Fatal. Diese Tests
// sichern, dass die Fatal-Signatur stabil bleibt und Passwörter/Tokens aus der Nachricht fliegen —
// sonst kämen Secrets aus einer Stack-Trace-Meldung ins Log.
public class LogServiceFatalTests
{
    [Fact]
    public void Fatal_ohne_Exception_feuert_StatusUpdated_mit_FATAL_Prefix()
    {
        string? captured = null;
        void Handler(string s) => captured = s;
        LogService.StatusUpdated += Handler;
        try
        {
            LogService.Fatal("Testmeldung");
        }
        finally
        {
            LogService.StatusUpdated -= Handler;
        }

        Assert.Equal("FATAL: Testmeldung", captured);
    }

    [Fact]
    public void Fatal_mit_Exception_feuert_StatusUpdated()
    {
        string? captured = null;
        void Handler(string s) => captured = s;
        LogService.StatusUpdated += Handler;
        try
        {
            LogService.Fatal("Nachricht", new InvalidOperationException("boom"));
        }
        finally
        {
            LogService.StatusUpdated -= Handler;
        }

        Assert.Equal("FATAL: Nachricht", captured);
    }

    [Fact]
    public void Fatal_maskiert_Secrets_im_Message_wie_Error()
    {
        string? captured = null;
        void Handler(string s) => captured = s;
        LogService.StatusUpdated += Handler;
        try
        {
            LogService.Fatal("crash mit api_key=abc123 im payload");
        }
        finally
        {
            LogService.StatusUpdated -= Handler;
        }

        Assert.NotNull(captured);
        Assert.DoesNotContain("abc123", captured);
        Assert.Contains("[REDACTED]", captured);
    }
}
