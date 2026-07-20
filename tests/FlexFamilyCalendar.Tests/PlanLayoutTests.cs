using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlanLayoutTests
{
    [Fact]
    public void OrderPeople_WhenNoPlanOrderSet_FallsBackToRoleThenName()
    {
        // PlanOrder-Default ist bei allen gleich (100) → Rollen-Rang + Name greifen.
        var users = new[]
        {
            new User { DisplayName = "Zoe", Category = PersonCategory.Employee },
            new User { DisplayName = "Bob", Category = PersonCategory.Parent },
            new User { DisplayName = "Alice", Category = PersonCategory.Child },
            new User { DisplayName = "Yan", Category = PersonCategory.AuPair },
            new User { DisplayName = "Anna", Category = PersonCategory.Parent },
        };

        var ordered = PlanLayout.OrderPeople(users).Select(u => u.DisplayName).ToArray();

        Assert.Equal(new[] { "Anna", "Bob", "Yan", "Zoe", "Alice" }, ordered);
    }

    [Fact]
    public void OrderPeople_PlanOrder_OverridesRoleAndName()
    {
        // Admin hat die Reihenfolge frei gesetzt — PlanOrder gewinnt vor Rolle und Name.
        var users = new[]
        {
            new User { DisplayName = "Zoe",   Category = PersonCategory.Employee, PlanOrder = 0 },
            new User { DisplayName = "Bob",   Category = PersonCategory.Parent,   PlanOrder = 1 },
            new User { DisplayName = "Alice", Category = PersonCategory.Child,    PlanOrder = 2 },
            new User { DisplayName = "Yan",   Category = PersonCategory.AuPair,   PlanOrder = 3 },
        };

        var ordered = PlanLayout.OrderPeople(users).Select(u => u.DisplayName).ToArray();

        Assert.Equal(new[] { "Zoe", "Bob", "Alice", "Yan" }, ordered);
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
