using Avalonia;
using Avalonia.Controls;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Controls;

/// <summary>
/// Arrangiert Kind-Container nach der Start-/Endzeit ihres <see cref="CalendarEntry"/>-DataContext:
/// vertikale Position/Höhe aus der Uhrzeit, überlappende Einträge nebeneinander in Spuren.
/// Bewusst kein virtualisierendes Panel — alle Container müssen für das Arrange realisiert sein.
/// </summary>
public class DayTimelinePanel : Panel
{
    private const double Gap = 2.0;
    private const double MinEntryHeight = 22.0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        foreach (var child in Children)
            child.Measure(new Size(width, CalendarMetrics.DayHeight));
        return new Size(width, CalendarMetrics.DayHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var items = new List<(Control Child, CalendarEntry Entry)>();
        foreach (var child in Children)
            if (child.DataContext is CalendarEntry e)
                items.Add((child, e));

        var lanes = AssignLanes(items.Select(i => i.Entry).ToList());

        for (int i = 0; i < items.Count; i++)
        {
            var (child, entry) = items[i];
            var (laneIndex, laneCount) = lanes[i];

            var laneWidth = finalSize.Width / Math.Max(1, laneCount);
            var x = laneIndex * laneWidth + Gap;
            var width = Math.Max(0, laneWidth - 2 * Gap);

            var y = entry.StartTime.TotalHours * CalendarMetrics.PixelsPerHour;
            var rawHeight = (entry.EndTime - entry.StartTime).TotalHours * CalendarMetrics.PixelsPerHour;
            var height = Math.Max(MinEntryHeight, rawHeight - Gap);

            child.Arrange(new Rect(x, y, width, height));
        }

        return new Size(finalSize.Width, CalendarMetrics.DayHeight);
    }

    /// <summary>
    /// Reine Spurenzuweisung. Greedy: nach Startzeit sortiert, jeder Eintrag in die erste Spur,
    /// deren letzter Eintrag spätestens zu seiner Startzeit endet. Die Spurenzahl eines
    /// Überlappungs-Clusters bestimmt die Breite aller darin liegenden Einträge.
    /// Rückgabe in Eingabereihenfolge: (laneIndex, laneCountImCluster).
    /// </summary>
    public static IReadOnlyList<(int LaneIndex, int LaneCount)> AssignLanes(IReadOnlyList<CalendarEntry> entries)
    {
        var n = entries.Count;
        var result = new (int, int)[n];
        if (n == 0) return result;

        // nach Start sortierte Indizes
        var order = Enumerable.Range(0, n)
            .OrderBy(i => entries[i].StartTime)
            .ThenBy(i => entries[i].EndTime)
            .ToList();

        var laneIndexByOrig = new int[n];
        var laneEnds = new List<TimeSpan>();          // Endzeit pro aktiver Spur
        var clusterMembers = new List<int>();         // Original-Indizes des aktuellen Clusters
        var clusterEnd = TimeSpan.MinValue;

        void FlushCluster()
        {
            var count = Math.Max(1, laneEnds.Count);
            foreach (var orig in clusterMembers)
                result[orig] = (laneIndexByOrig[orig], count);
            clusterMembers.Clear();
            laneEnds.Clear();
            clusterEnd = TimeSpan.MinValue;
        }

        foreach (var orig in order)
        {
            var entry = entries[orig];

            // Cluster endet, sobald ein Eintrag erst nach dem bisherigen Cluster-Ende beginnt
            if (clusterMembers.Count > 0 && entry.StartTime >= clusterEnd)
                FlushCluster();

            // erste freie Spur suchen
            int lane = -1;
            for (int l = 0; l < laneEnds.Count; l++)
            {
                if (laneEnds[l] <= entry.StartTime) { lane = l; break; }
            }
            if (lane == -1)
            {
                lane = laneEnds.Count;
                laneEnds.Add(entry.EndTime);
            }
            else
            {
                laneEnds[lane] = entry.EndTime;
            }

            laneIndexByOrig[orig] = lane;
            clusterMembers.Add(orig);
            if (entry.EndTime > clusterEnd) clusterEnd = entry.EndTime;
        }
        FlushCluster();

        return result;
    }
}
