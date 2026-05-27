using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class UserManagementTests
{
    private static (AuthService auth, InMemoryStorageService store) NewAuth()
    {
        var store = new InMemoryStorageService();
        return (new AuthService(store), store);
    }

    private static User MakeUser(string name, UserRole role = UserRole.User) => new()
    {
        Username = name,
        DisplayName = name,
        Role = role,
        Category = PersonCategory.AuPair,
        Language = "en"
    };

    [Fact]
    public async Task CreateUser_StoresBcryptHash_NotPlaintext()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("anna"), "geheim123");

        var users = await auth.GetUsersAsync();
        var anna = Assert.Single(users);
        Assert.NotEqual("geheim123", anna.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("geheim123", anna.PasswordHash));
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_Throws()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("anna"), "pw1");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => auth.CreateUserAsync(MakeUser("ANNA"), "pw2"));
    }

    [Fact]
    public async Task UpdateUser_ChangesFields()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("anna"), "pw1");
        var user = (await auth.GetUsersAsync()).Single();

        user.DisplayName = "Anna B.";
        user.Language = "de";
        user.WeeklyHoursQuota = 20;
        await auth.UpdateUserAsync(user);

        var updated = (await auth.GetUsersAsync()).Single();
        Assert.Equal("Anna B.", updated.DisplayName);
        Assert.Equal("de", updated.Language);
        Assert.Equal(20, updated.WeeklyHoursQuota);
    }

    [Fact]
    public async Task SetPassword_OldNoLongerVerifies_NewVerifies()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("anna"), "alt");
        var id = (await auth.GetUsersAsync()).Single().Id;

        await auth.SetPasswordAsync(id, "neu");

        var hash = (await auth.GetUsersAsync()).Single().PasswordHash;
        Assert.False(BCrypt.Net.BCrypt.Verify("alt", hash));
        Assert.True(BCrypt.Net.BCrypt.Verify("neu", hash));
    }

    [Fact]
    public async Task DeleteUser_RemovesUser()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("admin", UserRole.Admin), "pw");
        await auth.CreateUserAsync(MakeUser("anna"), "pw");
        var anna = (await auth.GetUsersAsync()).First(u => u.Username == "anna");

        await auth.DeleteUserAsync(anna.Id);

        Assert.DoesNotContain(await auth.GetUsersAsync(), u => u.Username == "anna");
    }

    [Fact]
    public async Task DeleteLastAdmin_IsBlocked()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("admin", UserRole.Admin), "pw");
        var admin = (await auth.GetUsersAsync()).Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.DeleteUserAsync(admin.Id));
    }

    [Fact]
    public async Task DemoteLastAdmin_IsBlocked()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("admin", UserRole.Admin), "pw");
        var admin = (await auth.GetUsersAsync()).Single();

        admin.Role = UserRole.User;
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.UpdateUserAsync(admin));
    }

    [Fact]
    public async Task SetUserLanguage_Persists()
    {
        var (auth, _) = NewAuth();
        await auth.CreateUserAsync(MakeUser("anna"), "pw");
        var id = (await auth.GetUsersAsync()).Single().Id;

        await auth.SetUserLanguageAsync(id, "de");

        Assert.Equal("de", (await auth.GetUsersAsync()).Single().Language);
    }
}
