namespace FlexFamilyCalendar.Models;

/// <summary>Ein berechneter Feiertag an einem bestimmten Datum. Der Name wird über einen i18n-Schlüssel lokalisiert.</summary>
public record Holiday(DateOnly Date, string NameKey);

/// <summary>Die 16 deutschen Bundesländer (ISO-3166-2-Kürzel ohne „DE-"-Präfix).</summary>
public enum GermanState
{
    BW, BY, BE, BB, HB, HH, HE, MV, NI, NW, RP, SL, SN, ST, SH, TH
}

/// <summary>Anzeigenamen der Bundesländer (Eigennamen, sprach-neutral).</summary>
public static class GermanStates
{
    public static readonly IReadOnlyDictionary<GermanState, string> Names = new Dictionary<GermanState, string>
    {
        [GermanState.BW] = "Baden-Württemberg",
        [GermanState.BY] = "Bayern",
        [GermanState.BE] = "Berlin",
        [GermanState.BB] = "Brandenburg",
        [GermanState.HB] = "Bremen",
        [GermanState.HH] = "Hamburg",
        [GermanState.HE] = "Hessen",
        [GermanState.MV] = "Mecklenburg-Vorpommern",
        [GermanState.NI] = "Niedersachsen",
        [GermanState.NW] = "Nordrhein-Westfalen",
        [GermanState.RP] = "Rheinland-Pfalz",
        [GermanState.SL] = "Saarland",
        [GermanState.SN] = "Sachsen",
        [GermanState.ST] = "Sachsen-Anhalt",
        [GermanState.SH] = "Schleswig-Holstein",
        [GermanState.TH] = "Thüringen",
    };

    public static GermanState Parse(string? code)
        => Enum.TryParse<GermanState>(code, ignoreCase: true, out var s) ? s : GermanState.BY;
}
