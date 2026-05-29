using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlanLayoutTests
{
    [Fact]
    public void OrderPeople_ByRole_ThenName()
    {
        var users = new[]
        {
            new User { DisplayName = "Zoe", Category = PersonCategory.Employee },
            new User { DisplayName = "Bob", Category = PersonCategory.Parent },
            new User { DisplayName = "Alice", Category = PersonCategory.Child },
            new User { DisplayName = "Yan", Category = PersonCategory.AuPair },
            new User { DisplayName = "Anna", Category = PersonCategory.Parent },
        };

        var ordered = PlanLayout.OrderPeople(users).Select(u => u.DisplayName).ToArray();

        // Reihenfolge: Eltern → Au-Pair → Angestellte → Kinder, je Gruppe nach Name
        Assert.Equal(new[] { "Anna", "Bob", "Yan", "Zoe", "Alice" }, ordered);
    }

    [Fact]
    public void CellEntries_FiltersByUser_IncludesAbsences_Sorted()
    {
        var timeline = new[]
        {
            new CalendarEntry { UserId = "u1", Type = EntryType.Work, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16) },
            new CalendarEntry { UserId = "u1", Type = EntryType.Activity, StartTime = TimeSpan.FromHours(16), EndTime = TimeSpan.FromHours(17) },
            new CalendarEntry { UserId = "u2", Type = EntryType.Work, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(15) },
        };
        var absences = new[]
        {
            new CalendarEntry { UserId = "u1", Type = EntryType.Vacation, StartTime = TimeSpan.FromHours(6), EndTime = TimeSpan.FromHours(7) },
            new CalendarEntry { UserId = "u2", Type = EntryType.SickLeave, StartTime = TimeSpan.FromHours(6), EndTime = TimeSpan.FromHours(7) },
        };

        var cell = PlanLayout.CellEntries(timeline, absences, "u1");

        Assert.Equal(3, cell.Count);                           // u2 ausgeschlossen
        Assert.Equal(EntryType.Vacation, cell[0].Type);        // 06:00
        Assert.Equal(EntryType.Work, cell[1].Type);            // 08:00
        Assert.Equal(EntryType.Activity, cell[2].Type);        // 16:00
    }
}
