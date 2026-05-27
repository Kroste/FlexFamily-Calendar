namespace FlexFamilyCalendar.Models;

/// <summary>Reine Akkumulation des laufenden Stundensaldos (UI-unabhängig, testbar).</summary>
public static class HoursAccount
{
    /// <summary>
    /// Laufende Salden: result[i] = opening + Σ monthlyDiffs[0..i].
    /// monthlyDiffs in chronologischer Reihenfolge (Ist − Soll je Monat).
    /// </summary>
    public static IReadOnlyList<double> RunningBalance(double opening, IEnumerable<double> monthlyDiffs)
    {
        var result = new List<double>();
        var balance = opening;
        foreach (var diff in monthlyDiffs)
        {
            balance += diff;
            result.Add(balance);
        }
        return result;
    }
}
