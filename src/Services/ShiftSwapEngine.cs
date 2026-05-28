using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine, testbare Logik des Schichttauschs: Prüfung und Umbuchung von Arbeitsschichten zwischen
/// zwei Mitarbeitern. UI- und Persistenz-unabhängig.
/// </summary>
public static class ShiftSwapEngine
{
    /// <summary>
    /// Prüft, ob ein Tausch angewendet werden darf. Gibt einen i18n-Fehlerschlüssel zurück
    /// (Swap_ErrorStale / Swap_ErrorFinalized / Swap_ErrorOverlap) oder null, wenn alles ok ist.
    /// Bei Tausch am selben Tag muss derselbe CalendarDay als <paramref name="fromDay"/> und
    /// <paramref name="toDay"/> übergeben werden.
    /// </summary>
    public static string? Validate(ShiftSwapRequest req, CalendarDay fromDay, CalendarDay? toDay)
    {
        var fromEntry = fromDay.Entries.FirstOrDefault(e => e.Id == req.FromEntryId);
        if (fromEntry == null) return "Swap_ErrorStale";
        if (fromDay.IsFinalized) return "Swap_ErrorFinalized";

        var assignments = new Dictionary<string, string> { [req.FromEntryId] = req.ToUserId };

        if (req.Mode == SwapMode.Exchange)
        {
            if (toDay == null || string.IsNullOrEmpty(req.ToEntryId)) return "Swap_ErrorStale";
            var toEntry = toDay.Entries.FirstOrDefault(e => e.Id == req.ToEntryId);
            if (toEntry == null) return "Swap_ErrorStale";
            if (toDay.IsFinalized) return "Swap_ErrorFinalized";
            assignments[req.ToEntryId] = req.FromUserId;
        }

        var affectedDays = new List<CalendarDay> { fromDay };
        if (req.Mode == SwapMode.Exchange && toDay != null && !ReferenceEquals(toDay, fromDay))
            affectedDays.Add(toDay);

        foreach (var day in affectedDays)
            foreach (var userId in new[] { req.FromUserId, req.ToUserId })
            {
                // Eintragsmenge dieses Nutzers an diesem Tag NACH der Umbuchung
                var entries = day.Entries.Where(e => assignments.GetValueOrDefault(e.Id, e.UserId) == userId);
                if (WorkTimeRules.WorkOverlaps(entries).Count > 0)
                    return "Swap_ErrorOverlap";
            }

        return null;
    }

    /// <summary>Wendet den Tausch an (mutiert die übergebenen Tage). Setzt voraus, dass <see cref="Validate"/> null lieferte.</summary>
    public static void Apply(ShiftSwapRequest req, CalendarDay fromDay, CalendarDay? toDay)
    {
        var fromEntry = fromDay.Entries.First(e => e.Id == req.FromEntryId);

        if (req.Mode == SwapMode.GiveAway)
        {
            Assign(fromEntry, req.ToUserId, req.ToUserName);
            return;
        }

        var counterDay = toDay ?? fromDay;
        var toEntry = counterDay.Entries.First(e => e.Id == req.ToEntryId);
        Assign(fromEntry, req.ToUserId, req.ToUserName);
        Assign(toEntry, req.FromUserId, req.FromUserName);
    }

    private static void Assign(CalendarEntry e, string userId, string userName)
    {
        e.UserId = userId;
        e.UserDisplayName = userName;
    }
}
