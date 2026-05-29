using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

public class UserMappingTests
{
    [Fact]
    public void ToDesktop_maps_all_fields()
    {
        var dto = new ServerUserDto("u1", "rike", "Rike", "r@x.de", "Admin", "Parent",
            WeeklyHoursQuota: 40, MaxWeeklyHours: 48, MaxDailyHours: 10, MinRestHours: 11,
            Color: "#FF0000", Language: "en");

        var u = UserMapping.ToDesktop(dto);

        Assert.Equal("rike", u.Username);
        Assert.Equal(UserRole.Admin, u.Role);
        Assert.Equal(PersonCategory.Parent, u.Category);
        Assert.Equal(40, u.WeeklyHoursQuota);
        Assert.Equal(11, u.MinRestHours);
        Assert.Equal("#FF0000", u.Color);
        Assert.Equal("en", u.Language);
    }

    [Fact]
    public void ToDesktop_defaults_language_when_missing()
    {
        var dto = new ServerUserDto("u1", "kid", "Kind", null, "User", "Child");
        var u = UserMapping.ToDesktop(dto);
        Assert.Equal("de", u.Language);
        Assert.Equal(PersonCategory.Child, u.Category);
        Assert.Equal(UserRole.User, u.Role);
    }

    [Fact]
    public void ToCreateBody_maps_role_category_and_password()
    {
        var u = new User
        {
            Username = "anna", DisplayName = "Anna", Email = "a@x.de",
            Role = UserRole.User, Category = PersonCategory.AuPair,
            WeeklyHoursQuota = 35, Color = "#00FF00", Language = "de"
        };

        var body = UserMapping.ToCreateBody(u, "geheim");

        Assert.Equal("anna", body.Username);
        Assert.Equal("geheim", body.Password);
        Assert.Equal("User", body.Role);
        Assert.Equal("AuPair", body.Category);
        Assert.Equal(35, body.WeeklyHoursQuota);
    }

    [Fact]
    public void ToCreateBody_admin_role_serialized_as_admin()
    {
        var u = new User { Username = "x", Role = UserRole.Admin, Category = PersonCategory.Parent };
        Assert.Equal("Admin", UserMapping.ToCreateBody(u, "p").Role);
    }

    [Fact]
    public void ToUpdateBody_has_no_password_and_keeps_fields()
    {
        var u = new User
        {
            Username = "bob", DisplayName = "Bob", Email = "b@x.de",
            Role = UserRole.User, Category = PersonCategory.Employee, MaxDailyHours = 8
        };

        var body = UserMapping.ToUpdateBody(u);

        Assert.Equal("bob", body.Username);
        Assert.Equal("Employee", body.Category);
        Assert.Equal(8, body.MaxDailyHours);
    }
}
