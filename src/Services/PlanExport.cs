using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Eine Zeile im Export: <paramref name="Time"/> (leer bei Abwesenheiten) + Beschreibung.</summary>
public record PlanExportLine(string Time, string Text);

public record PlanExportDay(
    string DayName, string DateLabel, string Holiday, string Note,
    IReadOnlyList<PlanExportLine> Absences, IReadOnlyList<PlanExportLine> Shifts);

public record WeekExport(string Title, string WeekLabel, string GeneratedLabel, IReadOnlyList<PlanExportDay> Days);

/// <summary>
/// Reine Aufbereitung der Wochen-Daten für den Export (UI- und Render-unabhängig, testbar).
/// Die Typ-Beschriftung wird als Funktion übergeben, damit die Lokalisierung außerhalb bleibt.
/// </summary>
public static class PlanExportBuilder
{
    /// <summary>Zeile für eine Schicht/Aktivität: Zeit + (Kategorie oder Typ) + ggf. Titel + Person.</summary>
    public static PlanExportLine ShiftLine(CalendarEntry e, Func<EntryType, string> typeLabel)
    {
        var label = e.HasActivity ? e.ActivityName : typeLabel(e.DisplayType);
        if (!string.IsNullOrEmpty(e.DisplayTitle))
            label += $" · {e.DisplayTitle}";
        if (!string.IsNullOrEmpty(e.UserDisplayName))
            label += $" – {e.UserDisplayName}";

        var time = e.IsContinuation ? $"↳ {e.TimeRange}" : e.TimeRange;
        return new PlanExportLine(time, label);
    }

    /// <summary>Zeile für eine Abwesenheit: Person + Typ (+ Zeitraum), ohne Uhrzeit.</summary>
    public static PlanExportLine AbsenceLine(CalendarEntry e, Func<EntryType, string> typeLabel)
    {
        var span = string.IsNullOrEmpty(e.AbsenceSpanLabel) ? "" : $" ({e.AbsenceSpanLabel})";
        return new PlanExportLine("", $"{e.UserDisplayName} – {typeLabel(e.DisplayType)}{span}");
    }
}
