using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

public enum MoveCopyAction { Move, Copy }

/// <summary>
/// Beschreibt die Storage-Operationen, die ein Drag&amp;Drop-Move bzw. -Copy auslöst.
/// <see cref="Delete"/> ist nur bei Move gesetzt; <see cref="Save"/> hält den (geklonten) Eintrag
/// mit neuem Benutzer/Datum, der gespeichert werden soll.
/// </summary>
public record MoveCopyPlan(
    CalendarEntry? Delete,
    DateOnly? DeleteFromDate,
    CalendarEntry Save,
    DateOnly SaveToDate);

/// <summary>
/// Pure Logik für „Schicht per Drag&amp;Drop auf eine andere Person/anderen Tag verschieben oder kopieren".
/// UI-unabhängig und vollständig testbar.
/// </summary>
public static class EntryMoveCopy
{
    /// <summary>Welche Eintrags-Typen lassen sich per Drag bewegen?
    /// Abwesenheiten (Urlaub/Krank/Abwesend) sind Bereich-Einträge mit eigenem Workflow.
    /// Wiederkehrende Projektionen sind read-only Overlays und nicht persistiert.</summary>
    public static bool CanDrag(CalendarEntry entry)
    {
        if (entry.IsRecurring) return false;
        if (EntryTypeInfo.IsAbsence(entry.Type)) return false;
        return true;
    }

    /// <summary>
    /// Erzeugt den Plan für eine Drop-Operation. Gibt null zurück, wenn die Operation nicht
    /// erlaubt ist (z.B. wiederkehrend, Abwesenheit, gleiche Quelle und gleiches Ziel).
    /// </summary>
    public static MoveCopyPlan? Plan(
        CalendarEntry source,
        DateOnly sourceDate,
        DateOnly targetDate,
        string targetUserId,
        string targetUserDisplayName,
        MoveCopyAction action)
    {
        if (!CanDrag(source)) return null;
        if (string.IsNullOrEmpty(targetUserId)) return null;
        if (sourceDate == targetDate && source.UserId == targetUserId) return null;

        var clone = Clone(source);
        clone.UserId = targetUserId;
        clone.UserDisplayName = targetUserDisplayName;
        // Immer eine neue Id, damit beim Verschieben die Lösch-Operation des Originals
        // den neuen Eintrag nicht erwischt (gleiche Id würde sonst nach Add wieder rausfallen).
        clone.Id = Guid.NewGuid().ToString();

        return action switch
        {
            MoveCopyAction.Copy => new MoveCopyPlan(null, null, clone, targetDate),
            MoveCopyAction.Move => new MoveCopyPlan(source, sourceDate, clone, targetDate),
            _ => null
        };
    }

    private static CalendarEntry Clone(CalendarEntry e) => new()
    {
        UserId = e.UserId,
        UserDisplayName = e.UserDisplayName,
        Type = e.Type,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        Title = e.Title,
        Notes = e.Notes,
        ActivityTypeId = e.ActivityTypeId,
    };
}
