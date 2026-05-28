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
    /// <summary>Ein Zellen-Eintrag: Uhrzeit (bzw. Abwesenheits-Zeitraum) + Kategorie/Typ (+ Titel), in Personenfarbe.</summary>
    public static PlanCellEntry CellEntry(CalendarEntry e, Func<EntryType, string> typeLabel)
    {
        var label = e.HasActivity ? e.ActivityName : typeLabel(e.DisplayType);
        if (!string.IsNullOrEmpty(e.DisplayTitle))
            label += $" · {e.DisplayTitle}";
        var time = e.IsAbsenceDisplay ? e.AbsenceSpanLabel : e.TimeRange;
        return new PlanCellEntry(string.IsNullOrEmpty(e.OwnerColor) ? "#7F8C8D" : e.OwnerColor, time, label);
    }
}
