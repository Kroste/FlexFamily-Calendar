using System.IO.Pipes;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

// Zweitstarts müssen scheitern, seit die App ein Tray-Icon hat und im lokalen Modus auf
// festen JSON-Dateien arbeitet. Jeder Test nutzt einen eigenen Pipe-Namen, damit die
// Instanzen sich nicht gegenseitig sehen und der Lauf parallelisierbar bleibt.
public class SingleInstanceGuardTests
{
    private static string UniqueName() => "test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void Erster_Start_bekommt_den_Zuschlag()
    {
        using var guard = new SingleInstanceGuard(UniqueName());
        Assert.True(guard.TryClaim());
    }

    [Fact]
    public void Zweiter_Start_wird_abgewiesen()
    {
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);
        Assert.True(first.TryClaim());

        using var second = new SingleInstanceGuard(name);
        Assert.False(second.TryClaim());
    }

    [Fact]
    public void Nach_Dispose_der_ersten_Instanz_ist_der_Name_wieder_frei()
    {
        var name = UniqueName();
        var first = new SingleInstanceGuard(name);
        Assert.True(first.TryClaim());
        first.Dispose();

        using var second = new SingleInstanceGuard(name);
        Assert.True(second.TryClaim());
    }

    [Fact]
    public async Task Zweitstart_loest_die_Aktivierung_der_ersten_Instanz_aus()
    {
        var name = UniqueName();
        using var primary = new SingleInstanceGuard(name);
        Assert.True(primary.TryClaim());

        var activated = new TaskCompletionSource();
        primary.ActivationRequested += () => activated.TrySetResult();

        using var secondary = new SingleInstanceGuard(name);
        Assert.False(secondary.TryClaim());
        secondary.NotifyPrimary();

        // Das Event kommt vom Threadpool — mit Timeout warten statt blind zu pollen.
        var done = await Task.WhenAny(activated.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Same(activated.Task, done);
    }

    [Fact]
    public void Verschiedene_Benutzer_blockieren_sich_nicht()
    {
        // Der Pipe-Name trägt den Benutzernamen: auf einem gemeinsam genutzten Rechner darf
        // die Instanz des einen nicht den Start des anderen verhindern.
        var suffix = Guid.NewGuid().ToString("N");
        using var anna = new SingleInstanceGuard("anna-" + suffix);
        using var bert = new SingleInstanceGuard("bert-" + suffix);

        Assert.True(anna.TryClaim());
        Assert.True(bert.TryClaim());
    }

    [Fact]
    public void Doppeltes_Dispose_ist_harmlos()
    {
        var guard = new SingleInstanceGuard(UniqueName());
        guard.TryClaim();

        guard.Dispose();
        guard.Dispose();
    }

    [Fact]
    public async Task Ein_fremder_lauschender_Server_verhindert_den_Zuschlag()
    {
        // Ein von Hand gebauter Server belegt den Namen und lauscht; TryClaim darf den Zuschlag
        // dann nicht geben — egal über welchen der beiden Wege es das erkennt.
        //
        // WICHTIG zur Reichweite: der eigentliche Bug der ersten Fassung lässt sich hier NICHT
        // nachstellen. Sie verließ sich allein auf die IOException aus dem Server-Konstruktor.
        // Innerhalb eines Prozesses kennt .NET den Pipe-Namen und wirft zuverlässig, deshalb
        // sind alle Tests dieser Klasse auch mit der kaputten Fassung grün. Prozessübergreifend
        // bildet .NET Named Pipes unter Linux/macOS auf Unix-Domain-Sockets ab und bindet die
        // vorhandene Datei einfach neu: dort bekam der Zweitstart seinen Server und hielt sich
        // für die erste Instanz — zwei vollständige Apps samt zwei Tray-Icons nebeneinander.
        //
        // Belegt wurde das mit zwei echten Prozessen aus einem `dotnet publish`-Output; der
        // Zweitstart muss mit Exitcode 0 enden und im Log der ersten Instanz muss
        // "Zweitstart erkannt" stehen. Diese Prüfung gehört bei Änderungen an TryClaim
        // wiederholt, ein grüner Testlauf allein reicht dafür nicht.
        var name = "FlexFamilyCalendar.SingleInstance.foreign-" + Guid.NewGuid().ToString("N");
        using var foreign = new NamedPipeServerStream(name, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepting = foreign.WaitForConnectionAsync(cts.Token);

        // Der Guard hängt den Präfix selbst an, deshalb hier nur den Suffix übergeben.
        var suffix = name["FlexFamilyCalendar.SingleInstance.".Length..];
        using var guard = new SingleInstanceGuard(suffix);

        Assert.False(guard.TryClaim());

        // Aufräumen: die Verbindung aus CanConnectToPrimary abschließen lassen.
        try { await accepting; } catch (OperationCanceledException) { /* nie verbunden */ }
    }

    [Fact]
    public void NotifyPrimary_ohne_laufende_Instanz_wirft_nicht()
    {
        // Die primäre Instanz kann zwischen TryClaim und NotifyPrimary weg sein. Der Zweitstart
        // beendet sich ohnehin — ein Fehlerdialog wäre hier nur Lärm.
        using var guard = new SingleInstanceGuard(UniqueName());
        guard.NotifyPrimary();
    }
}
