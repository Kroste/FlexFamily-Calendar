using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

public class AuthService
{
    private readonly StorageService _storage;

    public AuthService(StorageService storage) => _storage = storage;

    public async Task<User?> LoginAsync(string username, string password)
    {
        LogService.Debug("Anmeldeversuch für Benutzer: {0}", username);
        var users = await _storage.LoadUsersAsync();
        var user = users.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            LogService.Warn("Anmeldung fehlgeschlagen: Benutzer '{0}' nicht gefunden", username);
            return null;
        }
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            LogService.Warn("Anmeldung fehlgeschlagen: Falsches Kennwort für '{0}'", username);
            return null;
        }

        LogService.Info("Anmeldung erfolgreich: {0} (Rolle: {1})", username, user.Role);
        return user;
    }

    public async Task<bool> HasAnyUsersAsync()
        => (await _storage.LoadUsersAsync()).Count > 0;

    /// <summary>Merkt den Benutzernamen für Auto-Login (null/leer = Merker löschen). Kein Passwort.</summary>
    public async Task SetRememberedUsernameAsync(string? username)
    {
        var settings = await _storage.LoadSettingsAsync();
        settings.RememberedUsername = username ?? "";
        await _storage.SaveSettingsAsync(settings);
        LogService.Info(string.IsNullOrEmpty(settings.RememberedUsername)
            ? "Login merken deaktiviert"
            : $"Login merken aktiviert für {settings.RememberedUsername}");
    }

    /// <summary>Liefert den gemerkten Benutzer oder null. Leert den Merker, falls der Benutzer fehlt.</summary>
    public async Task<User?> GetRememberedUserAsync()
    {
        var settings = await _storage.LoadSettingsAsync();
        if (string.IsNullOrEmpty(settings.RememberedUsername))
            return null;

        var users = await _storage.LoadUsersAsync();
        var user = users.FirstOrDefault(u =>
            u.Username.Equals(settings.RememberedUsername, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            LogService.Warn("Gemerkter Benutzer '{0}' existiert nicht mehr — Merker geleert",
                settings.RememberedUsername);
            settings.RememberedUsername = "";
            await _storage.SaveSettingsAsync(settings);
        }
        return user;
    }

    public async Task CreateUserAsync(string username, string password, string displayName, UserRole role)
    {
        var users = await _storage.LoadUsersAsync();
        if (users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Benutzer '{username}' existiert bereits.");

        users.Add(new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName
        });
        await _storage.SaveUsersAsync(users);
        LogService.Info("Benutzer angelegt: {0} (Rolle: {1})", username, role);
    }
}
