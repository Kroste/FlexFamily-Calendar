namespace FlexFamilyCalendar.Services.Update;

/// <summary>
/// Schmaler SemVer-Vergleich für GitHub-Release-Tags (z.B. "v1.2.3", "1.2.3", "v1.2.3-test").
/// Vergleicht major.minor.patch numerisch; ein vorhandenes Pre-Release-Suffix (-test, -rc.1)
/// gilt als KLEINER als die gleiche Version ohne Suffix (SemVer 2.0 §11).
/// </summary>
public static class VersionCompare
{
    /// <summary>-1 = a kleiner, 0 = gleich, +1 = a größer. Ungültige Eingaben werden als 0.0.0 behandelt.</summary>
    public static int Compare(string a, string b)
    {
        var (ma, na, pa, prA) = Parse(a);
        var (mb, nb, pb, prB) = Parse(b);
        if (ma != mb) return ma.CompareTo(mb);
        if (na != nb) return na.CompareTo(nb);
        if (pa != pb) return pa.CompareTo(pb);
        // gleicher numerischer Anteil: kein Pre-Release > Pre-Release
        var aHas = !string.IsNullOrEmpty(prA);
        var bHas = !string.IsNullOrEmpty(prB);
        if (aHas && !bHas) return -1;
        if (!aHas && bHas) return 1;
        return string.CompareOrdinal(prA, prB);
    }

    public static bool IsNewer(string candidate, string baseline) => Compare(candidate, baseline) > 0;

    /// <summary>Tag wie "v1.2.3-test" → (1, 2, 3, "test").</summary>
    private static (int Major, int Minor, int Patch, string PreRelease) Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return (0, 0, 0, "");
        var v = version.Trim().TrimStart('v', 'V');
        var dash = v.IndexOf('-');
        var pre = dash >= 0 ? v[(dash + 1)..] : "";
        var numeric = dash >= 0 ? v[..dash] : v;
        // numerische Teile robust parsen — fehlende Stellen = 0.
        var parts = numeric.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int Get(int i) => i < parts.Length && int.TryParse(parts[i], out var x) ? x : 0;
        return (Get(0), Get(1), Get(2), pre);
    }
}
