using System.Text.Json;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Gemeinsame Datei-Primitiven für die JSON-Ablage des lokalen Speicher-Modus.
///
/// Zwei Regeln aus dem Kroste-Standard, die der <see cref="StorageService"/> vorher
/// beide verletzt hat:
///
/// 1. <b>Atomar schreiben.</b> Ein <c>WriteAllText</c> direkt aufs Ziel lässt bei Absturz
///    oder Stromausfall mitten im Schreiben eine halbe Datei zurück. Erst nach
///    <c>&lt;datei&gt;.tmp</c>, dann <c>File.Move(tmp, ziel, overwrite: true)</c> — das Move
///    ist atomar. Bei <c>users.json</c> hätte das sonst alle Benutzer samt Passwort-Hashes
///    gekostet.
///
/// 2. <b>Defekte Daten nicht still verlieren.</b> Ließ sich eine Datei nicht deserialisieren,
///    flog die Exception bis in die UI — und ein späterer Save hätte die kaputte Datei
///    endgültig überschrieben. Jetzt wandert sie nach <c>&lt;datei&gt;.broken</c> und bleibt
///    für die Rettung erhalten, die App startet leer weiter.
///
/// Bewusst NICHT quarantänisiert wird bei IO-Fehlern (Datei gesperrt, Netzlaufwerk kurz weg):
/// dort ist der Inhalt in Ordnung, nur gerade nicht lesbar. Ein Verschieben würde intakte
/// Daten wegräumen und genau den Verlust verursachen, den die Regel verhindern soll.
/// </summary>
internal static class JsonFileStore
{
    /// <summary>
    /// Liest und deserialisiert <paramref name="path"/>. Fehlt die Datei oder ist ihr Inhalt
    /// kein gültiges JSON, liefert <paramref name="fallback"/> das Ergebnis; im zweiten Fall
    /// wird die Datei vorher nach <c>.broken</c> gesichert.
    /// </summary>
    public static async Task<T> LoadAsync<T>(string path, Func<T> fallback)
    {
        if (!File.Exists(path)) return fallback();

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (IOException ex)
        {
            // Inhalt intakt, nur gerade nicht lesbar — nicht quarantänisieren.
            LogService.Error($"Datei {path} konnte nicht gelesen werden.", ex);
            return fallback();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions.Pretty) ?? fallback();
        }
        catch (JsonException ex)
        {
            LogService.Error($"Datei {path} enthält kein gültiges JSON.", ex);
            Quarantine(path);
            return fallback();
        }
    }

    /// <summary>Serialisiert <paramref name="value"/> und schreibt es atomar nach <paramref name="path"/>.</summary>
    public static async Task WriteAtomicAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, JsonOptions.Pretty));
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDeleteTemp(tmp);
            throw;
        }
    }

    /// <summary>
    /// Verschiebt eine nicht deserialisierbare Datei nach <c>&lt;datei&gt;.broken</c>. Schlägt
    /// das fehl, wird nur geloggt — der Aufrufer startet in jedem Fall leer weiter.
    /// </summary>
    public static void Quarantine(string path)
    {
        var broken = path + ".broken";
        try
        {
            File.Move(path, broken, overwrite: true);
            LogService.Warn("Defekte Datei nach {0} gesichert. Es wird leer weitergestartet.", broken);
        }
        catch (Exception ex)
        {
            LogService.Error($"Defekte Datei {path} konnte nicht nach {broken} gesichert werden.", ex);
        }
    }

    private static void TryDeleteTemp(string tmp)
    {
        try
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        catch (Exception ex)
        {
            LogService.Error($"Temporäre Datei {tmp} konnte nicht aufgeräumt werden.", ex);
        }
    }
}
