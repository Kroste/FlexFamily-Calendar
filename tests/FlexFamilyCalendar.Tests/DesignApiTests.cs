using FlexFamilyCalendar.DesignApi;
using FlexFamilyCalendar.ViewModels;
using System.Reflection;

namespace FlexFamilyCalendar.Tests;

// Die Design-Test-API kann die laufende App fernsteuern. Diese Tests decken die zwei
// Stellen ab, an denen ein Fehler teuer wird: die Startbedingungen (sie darf nicht
// versehentlich anspringen) und die Sperrliste (sie darf nichts Schreibendes durchlassen).
public class DesignApiOptionsTests
{
    [Fact]
    public void Ohne_api_port_bleibt_die_API_aus()
    {
        // Der Normalfall: die App startet ohne Schnittstelle.
        Assert.Null(DesignApiOptions.Parse([]));
        Assert.Null(DesignApiOptions.Parse(["--api-token", "geheim"]));
        Assert.Null(DesignApiOptions.Parse(["--api-allow-clicks"]));
    }

    [Fact]
    public void Port_und_Token_werden_uebernommen()
    {
        var o = DesignApiOptions.Parse(["--api-port", "8765", "--api-token", "geheim"]);

        Assert.NotNull(o);
        Assert.Equal(8765, o!.Port);
        Assert.Equal("geheim", o.Token);
    }

    [Fact]
    public void Klicks_sind_ohne_das_Flag_aus()
    {
        var ohne = DesignApiOptions.Parse(["--api-port", "8765"]);
        var mit = DesignApiOptions.Parse(["--api-port", "8765", "--api-allow-clicks"]);

        Assert.False(ohne!.AllowClicks);
        Assert.True(mit!.AllowClicks);
    }

    [Fact]
    public void Unvollstaendige_Argumente_kippen_nicht_um()
    {
        // "--api-token" am Ende ohne Wert darf keine IndexOutOfRange werfen.
        var o = DesignApiOptions.Parse(["--api-port", "8765", "--api-token"]);

        Assert.NotNull(o);
        Assert.Null(o!.Token);
    }

    [Fact]
    public void Unsinniger_Port_schaltet_die_API_ab()
    {
        Assert.Null(DesignApiOptions.Parse(["--api-port", "achttausend"]));
    }

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("10m", 600)]
    [InlineData("2h", 7200)]
    [InlineData("45", 45)]
    public void Dauer_wird_mit_und_ohne_Einheit_verstanden(string input, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), DesignApiOptions.ParseDuration(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bald")]
    [InlineData("10x")]
    public void Unverstaendliche_Dauer_heisst_kein_Auto_Shutdown(string? input)
    {
        Assert.Null(DesignApiOptions.ParseDuration(input));
    }
}

public class DestructiveGuardTests
{
    [Fact]
    public void Unbekannte_Namen_sind_gesperrt()
    {
        // Die Voreinstellung ist "nein". Ein vergessener Listeneintrag kostet hier einen
        // blockierten Testklick — andersherum einen gelöschten Kalendertag in der echten DB.
        Assert.False(DestructiveGuard.IsAllowed("IrgendeinNeuerCommand"));
        Assert.False(DestructiveGuard.IsAllowed(""));
        Assert.False(DestructiveGuard.IsAllowed(null));
    }

    [Fact]
    public void Harmlose_Navigation_ist_erlaubt()
    {
        Assert.True(DestructiveGuard.IsAllowed("OpenInfo"));
        Assert.True(DestructiveGuard.IsAllowed("NextWeek"));
    }

    [Theory]
    [InlineData("MailPlan")]          // verschickt an die ganze Familie
    [InlineData("ToggleFinalizeWeek")] // gibt fremde Einträge frei
    [InlineData("DeleteUser")]
    [InlineData("CopyWeekToNext")]
    [InlineData("Install")]           // tauscht die laufende Anwendung aus
    [InlineData("Logout")]
    public void Schreibende_und_kommunizierende_Aktionen_sind_gesperrt(string name)
    {
        Assert.False(DestructiveGuard.IsAllowed(name));
    }

    [Fact]
    public void Bestaetigungs_Schaltflaechen_sind_mitgesperrt()
    {
        // Sonst sperrt man den Command und klickt die Warnung daneben einfach weg.
        Assert.False(DestructiveGuard.IsAllowed("ConfirmButton"));
        Assert.False(DestructiveGuard.IsAllowed("DeleteButton"));
        Assert.False(DestructiveGuard.IsAllowed("SendButton"));
    }

    [Fact]
    public void Keine_Ueberschneidung_zwischen_erlaubt_und_gesperrt()
    {
        var beides = DestructiveGuard.Safe.Intersect(DestructiveGuard.Blocked, StringComparer.Ordinal).ToList();
        Assert.True(beides.Count == 0, "In beiden Listen: " + string.Join(", ", beides));
    }

    [Fact]
    public void Jeder_Command_des_Hauptfensters_ist_ausdruecklich_eingeordnet()
    {
        // Die Listen sind handgepflegt und veralten sonst still: eine neue Aktion im
        // MainWindowViewModel wäre automatisch gesperrt, aber niemand hätte sie bewusst
        // bewertet. Dieser Test erzwingt die Entscheidung.
        var commands = typeof(MainWindowViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => typeof(System.Windows.Input.ICommand).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name.EndsWith("Command", StringComparison.Ordinal)
                ? p.Name[..^"Command".Length]
                : p.Name)
            .ToList();

        Assert.NotEmpty(commands);

        var unbewertet = commands
            .Where(c => !DestructiveGuard.Safe.Contains(c) && !DestructiveGuard.Blocked.Contains(c))
            .ToList();

        Assert.True(unbewertet.Count == 0,
            "Weder als unbedenklich noch als gesperrt gelistet: " + string.Join(", ", unbewertet));
    }
}
