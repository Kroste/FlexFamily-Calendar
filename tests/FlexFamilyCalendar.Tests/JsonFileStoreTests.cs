using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

// Die zwei Persistenz-Regeln aus dem Kroste-Standard, die der StorageService vorher
// verletzt hat: atomar schreiben und defekte Dateien sichern statt überschreiben.
// JsonFileStore ist internal — der Zugriff läuft über InternalsVisibleTo in der src-csproj.
public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "ffc-jsonstore-" + Guid.NewGuid().ToString("N"));

    private string Path_(string name) => System.IO.Path.Combine(_dir, name);

    public JsonFileStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* Aufräumen ist best effort */ }
    }

    private sealed record Box(string Name, int Wert);

    [Fact]
    public async Task Fehlende_Datei_liefert_Fallback()
    {
        var result = await JsonFileStore.LoadAsync(Path_("gibtsnicht.json"), () => new Box("leer", 0));
        Assert.Equal(new Box("leer", 0), result);
    }

    [Fact]
    public async Task Schreiben_und_Lesen_ist_verlustfrei()
    {
        var file = Path_("box.json");
        await JsonFileStore.WriteAtomicAsync(file, new Box("Ümläute", 42));

        var result = await JsonFileStore.LoadAsync(file, () => new Box("fallback", -1));
        Assert.Equal(new Box("Ümläute", 42), result);
    }

    [Fact]
    public async Task Schreiben_laesst_keine_tmp_Datei_zurueck()
    {
        var file = Path_("box.json");
        await JsonFileStore.WriteAtomicAsync(file, new Box("a", 1));

        Assert.True(File.Exists(file));
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public async Task Schreiben_legt_fehlendes_Verzeichnis_an()
    {
        var file = Path_(System.IO.Path.Combine("2026", "KW34", "tag.json"));
        await JsonFileStore.WriteAtomicAsync(file, new Box("tief", 7));

        Assert.Equal(new Box("tief", 7), await JsonFileStore.LoadAsync(file, () => new Box("x", 0)));
    }

    [Fact]
    public async Task Kaputtes_JSON_wandert_nach_broken_und_bleibt_erhalten()
    {
        var file = Path_("kaputt.json");
        await File.WriteAllTextAsync(file, "{ das ist kein JSON", TestContext.Current.CancellationToken);

        var result = await JsonFileStore.LoadAsync(file, () => new Box("fallback", -1));

        Assert.Equal(new Box("fallback", -1), result);
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(file + ".broken"));
        // Entscheidend: der Originalinhalt ist gerettet, nicht überschrieben.
        Assert.Equal("{ das ist kein JSON", await File.ReadAllTextAsync(file + ".broken", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Zweites_kaputtes_JSON_ueberschreibt_die_alte_broken_Datei()
    {
        var file = Path_("kaputt.json");
        await File.WriteAllTextAsync(file, "erster Müll", TestContext.Current.CancellationToken);
        await JsonFileStore.LoadAsync(file, () => new Box("f", 0));

        await File.WriteAllTextAsync(file, "zweiter Müll", TestContext.Current.CancellationToken);
        await JsonFileStore.LoadAsync(file, () => new Box("f", 0));

        // Kein Absturz durch bereits existierende .broken-Datei (Move mit overwrite).
        Assert.Equal("zweiter Müll", await File.ReadAllTextAsync(file + ".broken", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ueberschreiben_einer_bestehenden_Datei_behaelt_nur_den_neuen_Stand()
    {
        var file = Path_("box.json");
        await JsonFileStore.WriteAtomicAsync(file, new Box("alt", 1));
        await JsonFileStore.WriteAtomicAsync(file, new Box("neu", 2));

        Assert.Equal(new Box("neu", 2), await JsonFileStore.LoadAsync(file, () => new Box("x", 0)));
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public async Task Leeres_JSON_null_faellt_auf_den_Fallback_zurueck()
    {
        var file = Path_("null.json");
        await File.WriteAllTextAsync(file, "null", TestContext.Current.CancellationToken);

        // "null" ist gültiges JSON, deserialisiert aber zu null — kein .broken, nur Fallback.
        Assert.Equal(new Box("fallback", -1), await JsonFileStore.LoadAsync(file, () => new Box("fallback", -1)));
        Assert.False(File.Exists(file + ".broken"));
    }
}
