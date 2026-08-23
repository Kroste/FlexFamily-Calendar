using System.Runtime.CompilerServices;
using FlexFamilyCalendar.Api.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace FlexFamilyCalendar.Api.Tests;

// Zweite Verteidigungslinie gegen Secrets im Log. Getestet wird über eine echte
// NLog-Konfiguration mit ${masked}, nicht gegen Transform() direkt — sonst würde ein
// Registrierungsfehler durchrutschen und im Log stünde am Ende nur "}".
public class MaskingLayoutRendererTests
{
    private static string Render(string message)
    {
        // Der ModuleInitializer registriert den Renderer beim Laden des Assemblys. Ein
        // typeof(...) lädt nur das Typ-Token und löst ihn NICHT aus — im Test muss der
        // Modul-Konstruktor erzwungen werden, sonst kennt NLog ${masked} nicht.
        RuntimeHelpers.RunModuleConstructor(typeof(MaskingLayoutRenderer).Module.ModuleHandle);

        var target = new MemoryTarget { Layout = "${masked:inner=${message}}" };
        var config = new LoggingConfiguration();
        config.AddRuleForAllLevels(target);

        var factory = new LogFactory { Configuration = config };
        factory.GetLogger("test").Info(message);
        return target.Logs.Single();
    }

    [Fact]
    public void Renderer_ist_registriert_und_gibt_die_Message_vollstaendig_aus()
    {
        // Regression: ein unbekanntes ${masked:…} verschluckt bei NLog das Message-Ende.
        Assert.Equal("Alles harmlos hier", Render("Alles harmlos hier"));
    }

    [Theory]
    [InlineData("{\"username\":\"lars\",\"password\":\"geheim123\"}", "geheim123")]
    [InlineData("{\"token\":\"eyJhbGciOiJIUzI1NiJ9.abc\"}", "eyJhbGciOiJIUzI1NiJ9.abc")]
    [InlineData("{\"apiKey\":\"sk-ant-42\"}", "sk-ant-42")]
    [InlineData("{\"Password\":\"GROSS\"}", "GROSS")]
    public void JSON_Felder_mit_Secrets_werden_maskiert(string message, string secret)
    {
        var result = Render(message);
        Assert.DoesNotContain(secret, result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void Connection_String_Passwort_wird_maskiert()
    {
        var result = Render("Host=db;Database=flexfamily;Username=ffc;Password=supergeheim");
        Assert.DoesNotContain("supergeheim", result);
        // Der Rest bleibt lesbar — sonst ist das Log für die Diagnose wertlos.
        Assert.Contains("Host=db", result);
        Assert.Contains("Username=ffc", result);
    }

    [Fact]
    public void Bearer_Token_wird_maskiert()
    {
        var result = Render("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.xyz");
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.xyz", result);
        Assert.Contains("Bearer ***", result);
    }

    [Fact]
    public void Harmlose_Woerter_mit_pass_werden_nicht_zerstoert()
    {
        // "passt" und "Passagier" dürfen nicht als Secret durchgehen — das Muster
        // verlangt ein "=" bzw. JSON-Doppelpunkt dahinter.
        var result = Render("Der Termin passt für den Passagier");
        Assert.Equal("Der Termin passt für den Passagier", result);
    }
}
