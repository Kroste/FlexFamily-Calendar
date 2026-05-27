using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class WeeklyHoursSelectionTests
{
    private static User U(string id, double quota) => new()
    {
        Id = id, Username = id, DisplayName = id, WeeklyHoursQuota = quota,
        Category = PersonCategory.Employee
    };

    [Fact]
    public void PersonalView_ReturnsOnlyCurrentUser_EvenWithoutQuota()
    {
        var me = U("me", 0);
        var all = new List<User> { me, U("a", 40), U("b", 20) };

        var result = WeeklyHoursCalculator.RelevantUsers(all, me, personalView: true).ToList();

        Assert.Single(result);
        Assert.Equal("me", result[0].Id);
    }

    [Fact]
    public void PlanningView_ReturnsAllWithQuota_Only()
    {
        var me = U("me", 0);
        var all = new List<User> { me, U("a", 40), U("b", 20) };

        var ids = WeeklyHoursCalculator.RelevantUsers(all, me, personalView: false)
            .Select(u => u.Id).ToList();

        Assert.Equal(new[] { "a", "b" }, ids.OrderBy(x => x));
        Assert.DoesNotContain("me", ids);
    }
}

public class WeeklyHoursViewModelTests
{
    [Fact]
    public void WithTarget_ShowsIstUndSoll()
    {
        var vm = new WeeklyHoursViewModel("Anna", 18, 20);
        Assert.True(vm.HasTarget);
        Assert.Equal("18 / 20 h", vm.Summary);
    }

    [Fact]
    public void WithoutTarget_ShowsOnlyIst()
    {
        var vm = new WeeklyHoursViewModel("Lars", 6, 0);
        Assert.False(vm.HasTarget);
        Assert.Equal("6 h", vm.Summary);
    }

    [Fact]
    public void OverTarget_IsOrange()
    {
        var vm = new WeeklyHoursViewModel("Anna", 25, 20);
        Assert.Equal("#E67E22", vm.BarColor);
    }
}
