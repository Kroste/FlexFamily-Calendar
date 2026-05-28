using FlexFamilyCalendar.Models;
using static FlexFamilyCalendar.Models.GermanState;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine, offline Berechnung der gesetzlichen Feiertage in Deutschland je Bundesland
/// (bundesweite + regionale, inkl. beweglicher über die Gauß'sche Osterformel). UI-unabhängig, testbar.
/// </summary>
public static class HolidayCalculator
{
    /// <summary>Ostersonntag eines Jahres (anonyme gregorianische / Meeus-Jones-Butcher-Formel).</summary>
    public static DateOnly Easter(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    /// <summary>Buß- und Bettag: der Mittwoch vor dem 23. November (immer 16.–22. Nov.).</summary>
    private static DateOnly RepentanceDay(int year)
    {
        var nov23 = new DateOnly(year, 11, 23);
        int back = ((int)nov23.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;
        if (back == 0) back = 7;
        return nov23.AddDays(-back);
    }

    public static IReadOnlyList<Holiday> ForYear(int year, GermanState state)
    {
        var easter = Easter(year);
        var list = new List<Holiday>();
        void Add(DateOnly date, string key) => list.Add(new Holiday(date, key));
        bool In(params GermanState[] states) => states.Contains(state);

        // Bundesweit
        Add(new DateOnly(year, 1, 1), "Holiday_NewYear");
        Add(easter.AddDays(-2), "Holiday_GoodFriday");
        Add(easter.AddDays(1), "Holiday_EasterMonday");
        Add(new DateOnly(year, 5, 1), "Holiday_LabourDay");
        Add(easter.AddDays(39), "Holiday_Ascension");
        Add(easter.AddDays(50), "Holiday_WhitMonday");
        Add(new DateOnly(year, 10, 3), "Holiday_GermanUnity");
        Add(new DateOnly(year, 12, 25), "Holiday_Christmas1");
        Add(new DateOnly(year, 12, 26), "Holiday_Christmas2");

        // Regional
        if (In(BW, BY, ST)) Add(new DateOnly(year, 1, 6), "Holiday_Epiphany");
        if (In(BE, MV)) Add(new DateOnly(year, 3, 8), "Holiday_WomensDay");
        if (In(BB)) Add(easter, "Holiday_EasterSunday");
        if (In(BB)) Add(easter.AddDays(49), "Holiday_WhitSunday");
        if (In(BW, BY, HE, NW, RP, SL)) Add(easter.AddDays(60), "Holiday_CorpusChristi");
        if (In(SL)) Add(new DateOnly(year, 8, 15), "Holiday_AssumptionDay");
        if (In(TH)) Add(new DateOnly(year, 9, 20), "Holiday_WorldChildrensDay");
        if (In(BB, HB, HH, MV, NI, SN, ST, SH, TH)) Add(new DateOnly(year, 10, 31), "Holiday_Reformation");
        if (In(BW, BY, NW, RP, SL)) Add(new DateOnly(year, 11, 1), "Holiday_AllSaints");
        if (In(SN)) Add(RepentanceDay(year), "Holiday_RepentanceDay");

        return list;
    }

    /// <summary>Feiertage im (inklusiven) Datumsbereich für ein Bundesland.</summary>
    public static IReadOnlyList<Holiday> ForRange(DateOnly from, DateOnly to, GermanState state)
    {
        var result = new List<Holiday>();
        for (int y = from.Year; y <= to.Year; y++)
            result.AddRange(ForYear(y, state).Where(h => h.Date >= from && h.Date <= to));
        return result;
    }
}
