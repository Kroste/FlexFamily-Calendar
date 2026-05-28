using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Ein farbig positionierter Block im Zeitraster (eine Schicht/Aktivität).</summary>
public record PlanBlock(
    double StartHour, double EndHour, string ColorHex, double Opacity,
    string TimeLabel, IReadOnlyList<string> Lines, int LaneIndex, int LaneCount);

/// <summary>Eine Abwesenheit als farbiger Chip im Tageskopf.</summary>
public record PlanChip(string ColorHex, string Text);

public record PlanExportDay(
    string DayName, string DateLabel, string Holiday, string Note,
    IReadOnlyList<PlanChip> Absences, IReadOnlyList<PlanBlock> Blocks);

public record WeekExport(string Title, string WeekLabel, string GeneratedLabel, IReadOnlyList<PlanExportDay> Days);

/// <summary>
/// Reine Aufbereitung der Wochen-Daten für den Raster-Export (UI-/Render-unabhängig, testbar).
/// Die Typ-Beschriftung wird als Funktion übergeben, damit die Lokalisierung außerhalb bleibt.
/// </summary>
public static class PlanExportBuilder
{
    private const string FallbackColor = "#7F8C8D";

    /// <summary>Bildet einen Tageseintrag auf einen Raster-Block ab (Position aus Start/Ende, Über-Mitternacht bis 24:00).</summary>
    public static PlanBlock BlockOf(CalendarEntry e, int laneIndex, int laneCount, Func<EntryType, string> typeLabel)
    {
        var lines = new List<string>();
        var primary = e.HasActivity ? e.ActivityName : typeLabel(e.DisplayType);
        if (!string.IsNullOrEmpty(primary)) lines.Add(primary);
        if (!string.IsNullOrEmpty(e.DisplayTitle)) lines.Add(e.DisplayTitle);
        if (!string.IsNullOrEmpty(e.UserDisplayName)) lines.Add(e.UserDisplayName);

        var time = (e.IsContinuation ? "» " : "") + e.TimeRange;
        return new PlanBlock(
            e.StartTime.TotalHours,
            e.CrossesMidnight ? 24.0 : e.EndTime.TotalHours,
            string.IsNullOrEmpty(e.OwnerColor) ? FallbackColor : e.OwnerColor,
            e.EffectiveOpacity,
            time, lines, laneIndex, laneCount);
    }

    /// <summary>Abwesenheits-Chip: Person + Typ (+ Zeitraum), in Personenfarbe.</summary>
    public static PlanChip ChipOf(CalendarEntry e, Func<EntryType, string> typeLabel)
    {
        var span = string.IsNullOrEmpty(e.AbsenceSpanLabel) ? "" : $" ({e.AbsenceSpanLabel})";
        return new PlanChip(
            string.IsNullOrEmpty(e.OwnerColor) ? FallbackColor : e.OwnerColor,
            $"{e.UserDisplayName} – {typeLabel(e.DisplayType)}{span}");
    }
}
