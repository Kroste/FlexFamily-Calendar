using FlexFamilyCalendar.Api.Users;

namespace FlexFamilyCalendar.Api.Tests;

public class UserRulesTests
{
    [Theory]
    [InlineData("Admin", "Admin")]
    [InlineData("admin", "User")]   // exakte Schreibweise erforderlich → sonst User
    [InlineData("User", "User")]
    [InlineData(null, "User")]
    [InlineData("", "User")]
    public void NormalizeRole_only_exact_admin_is_admin(string? input, string expected)
        => Assert.Equal(expected, UserRules.NormalizeRole(input));

    [Fact]
    public void Demoting_last_admin_is_blocked()
        => Assert.NotNull(UserRules.CheckRoleChange(targetIsCurrentlyAdmin: true, newRoleIsAdmin: false, totalAdmins: 1));

    [Fact]
    public void Demoting_admin_when_others_remain_is_allowed()
        => Assert.Null(UserRules.CheckRoleChange(targetIsCurrentlyAdmin: true, newRoleIsAdmin: false, totalAdmins: 2));

    [Fact]
    public void Keeping_admin_role_is_always_allowed()
        => Assert.Null(UserRules.CheckRoleChange(targetIsCurrentlyAdmin: true, newRoleIsAdmin: true, totalAdmins: 1));

    [Fact]
    public void Promoting_non_admin_is_allowed()
        => Assert.Null(UserRules.CheckRoleChange(targetIsCurrentlyAdmin: false, newRoleIsAdmin: true, totalAdmins: 1));

    [Fact]
    public void Deleting_last_admin_is_blocked()
        => Assert.NotNull(UserRules.CheckDelete(targetIsAdmin: true, totalAdmins: 1));

    [Fact]
    public void Deleting_admin_when_others_remain_is_allowed()
        => Assert.Null(UserRules.CheckDelete(targetIsAdmin: true, totalAdmins: 2));

    [Fact]
    public void Deleting_non_admin_is_allowed()
        => Assert.Null(UserRules.CheckDelete(targetIsAdmin: false, totalAdmins: 1));
}
