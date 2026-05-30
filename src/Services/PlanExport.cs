using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Ein Eintrag in einer Tabellenzelle: Personenfarbe + (Uhrzeit oder Abwesenheits-Zeitraum) + Bezeichnung.</summary>
public record PlanCellEntry(string ColorHex, string Time, string Label);

/// <summary>Spaltenkopf eines Wochentags (Datum + Feiertag).</summary>
public record PlanDayHeader(string DayName, string DateLabel, string Holiday);

/// <summary>Eine Personenzeile mit 7 Tageszellen (je Zelle eine Liste von Einträgen).</summary>
public record PlanPersonRow(string Name, string ColorHex, string Category,
    IReadOnlyList<IReadOnlyList<PlanCellEntry>> Cells);

public record WeekExport(
    string Title, string WeekLabel, string GeneratedLabel,
    IReadOnlyList<PlanDayHeader> Days, IReadOnlyList<PlanPersonRow> Rows, IReadOnlyList<string> Notes);

/// <summary>Reine Aufbereitung der Tabellen-Daten für den Export (UI-/Render-unabhängig, testbar).</summary>
public static class PlanExportBuilder
{
    /// <summary>
    /// Ein Zellen-Eintrag aus Sicht eines bestimmten Empfängers: Uhrzeit (bzw. Abwesenheits-Zeitraum)
    /// + Kategorie/Typ (+ Titel), in Personenfarbe. Fremde private Abwesenheiten (Krank/Urlaub) werden
    /// zu „Abwesend" ohne Grund maskiert (nur Admin oder der Betroffene sehen den Grund).
    /// </summary>
    public static PlanCellEntry CellEntry(CalendarEntry e, bool viewerIsAdmin, string viewerId, Func<EntryType, string> typeLabel)
    {
        var canSeeReason = viewerIsAdmin || e.UserId == viewerId;
        var displayType = EntryPrivacy.DisplayType(e.Type, canSeeReason);

        var label = e.HasActivity ? e.ActivityName : typeLabel(displayType);
        if (EntryPrivacy.ShowReason(e.Type, canSeeReason) && !string.IsNullOrEmpty(e.Title))
            label += $" · {e.Title}";

        var time = EntryTypeInfo.IsAbsence(displayType) ? e.AbsenceSpanLabel : e.TimeRange;
        return new PlanCellEntry(string.IsNullOrEmpty(e.OwnerColor) ? "#7F8C8D" : e.OwnerColor, time, label);
    }

    /// <summary>
    /// Personalisierter Tages-Hinweis aus Sicht des Empfängers:
    /// <list type="bullet">
    ///   <item>Hinweis ohne Adressat → für alle sichtbar.</item>
    ///   <item>Admin → sieht alles, auch wenn der Hinweis an jemand anderen adressiert ist.</item>
    ///   <item>Sonst nur sichtbar, wenn der Empfänger selbst der Adressat ist.</item>
    /// </list>
    /// </summary>
    public static string NoteFor(string rawNote, string? noteUserId, bool viewerIsAdmin, string viewerId)
    {
        if (string.IsNullOrWhiteSpace(rawNote)) return "";
        if (string.IsNullOrEmpty(noteUserId)) return rawNote;
        if (viewerIsAdmin) return rawNote;
        return noteUserId == viewerId ? rawNote : "";
    }
}
