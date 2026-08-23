namespace FlexFamilyCalendar.DesignApi;

/// <summary>
/// Startparameter der Design-Test-API. Kommt ausschließlich von der Kommandozeile —
/// bewusst nicht aus den App-Settings: eine Schnittstelle, die die laufende App
/// fernsteuern kann, soll man nicht versehentlich dauerhaft eingeschaltet lassen.
/// </summary>
/// <param name="Port">Loopback-Port. Ohne Port startet die API gar nicht.</param>
/// <param name="Token">Bearer-Token. Fehlt es, antwortet jede Anfrage mit 403.</param>
/// <param name="AutoShutdownAfter">Beendet die App nach dieser Zeit — verhindert Zombie-Instanzen nach abgebrochenen Skripten.</param>
/// <param name="AllowClicks">Schaltet <c>/click</c> frei. Ohne das Flag ist es komplett gesperrt.</param>
public sealed record DesignApiOptions(
    int Port,
    string? Token,
    TimeSpan? AutoShutdownAfter,
    bool AllowClicks)
{
    /// <summary>
    /// Liest die Optionen aus den Kommandozeilenargumenten. <c>null</c>, wenn <c>--api-port</c>
    /// fehlt — dann bleibt die API aus, was der Normalfall ist.
    /// </summary>
    public static DesignApiOptions? Parse(string[] args)
    {
        if (IntArg(args, "--api-port") is not { } port) return null;

        return new DesignApiOptions(
            Port: port,
            Token: StringArg(args, "--api-token")
                   ?? Environment.GetEnvironmentVariable("FFC_API_TOKEN"),
            AutoShutdownAfter: ParseDuration(StringArg(args, "--auto-shutdown-after")),
            AllowClicks: args.Contains("--api-allow-clicks", StringComparer.Ordinal));
    }

    private static int? IntArg(string[] args, string key)
        => int.TryParse(StringArg(args, key), out var v) ? v : null;

    private static string? StringArg(string[] args, string key)
    {
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Dauer als <c>30s</c>, <c>10m</c>, <c>2h</c> oder blanke Sekundenzahl.</summary>
    public static TimeSpan? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        var unit = s[^1];
        if (char.IsDigit(unit))
            return double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var sec)
                ? TimeSpan.FromSeconds(sec) : null;

        if (!double.TryParse(s[..^1], System.Globalization.CultureInfo.InvariantCulture, out var n))
            return null;

        return char.ToLowerInvariant(unit) switch
        {
            's' => TimeSpan.FromSeconds(n),
            'm' => TimeSpan.FromMinutes(n),
            'h' => TimeSpan.FromHours(n),
            _ => null,
        };
    }
}
