using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class UserRulesTests
{
    private static AuthService NewAuth() => new(new InMemoryStorageService());

    [Fact]
    public async Task Parent_KeepsRequestedRole_OnCreate()
    {
        // Seit v0.8.0 (CanFinalize-Trennung) sind Admin (Role) und Eltern (Category)
        // orthogonal — die Auto-Promotion „Eltern → Admin" wird nicht mehr erzwungen.
        var auth = NewAuth();
        await auth.CreateUserAsync(new User
        {
            Username = "papa", Category = PersonCategory.Parent, Role = UserRole.User
        }, "pw");

        Assert.Equal(UserRole.User, (await auth.GetUsersAsync()).Single().Role);
    }

    [Fact]
    public async Task Parent_KeepsRequestedRole_OnUpdate()
    {
        var auth = NewAuth();
        await auth.CreateUserAsync(new User { Username = "x", Category = PersonCategory.Employee, Role = UserRole.User }, "pw");
        await auth.CreateUserAsync(new User { Username = "admin", Category = PersonCategory.Employee, Role = UserRole.Admin }, "pw");

        var u = (await auth.GetUsersAsync()).First(x => x.Username == "x");
        u.Category = PersonCategory.Parent;
        u.Role = UserRole.User;
        await auth.UpdateUserAsync(u);

        Assert.Equal(UserRole.User, (await auth.GetUsersAsync()).First(x => x.Username == "x").Role);
    }

    [Fact]
    public async Task Child_CanBeCreated_WithoutPassword_AndHasNoHash()
    {
        var auth = NewAuth();
        await auth.CreateUserAsync(new User { Username = "kid", Category = PersonCategory.Child }, "");

        Assert.Equal("", (await auth.GetUsersAsync()).Single().PasswordHash);
    }

    [Fact]
    public async Task Child_CannotLogIn()
    {
        var auth = NewAuth();
        await auth.CreateUserAsync(new User { Username = "kid", Category = PersonCategory.Child }, "");

        Assert.Null(await auth.LoginAsync("kid", ""));
        Assert.Null(await auth.LoginAsync("kid", "irgendwas"));
    }

    [Fact]
    public async Task NonChild_WithoutPassword_Throws()
    {
        var auth = NewAuth();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => auth.CreateUserAsync(new User { Username = "anna", Category = PersonCategory.AuPair }, ""));
    }
}
