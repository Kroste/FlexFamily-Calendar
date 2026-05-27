using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

public class AuthService
{
    private readonly IStorageService _storage;

    public AuthService(IStorageService storage) => _storage = storage;

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
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            LogService.Warn("Anmeldung abgewiesen: '{0}' hat kein Anmeldekonto (z.B. Kind)", username);
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

    public async Task SetUserLanguageAsync(string userId, string language)
    {
        var users = await _storage.LoadUsersAsync();
        var user = users.FirstOrDefault(u => u.Id == userId);
        if (user == null || user.Language == language) return;
        user.Language = language;
        await _storage.SaveUsersAsync(users);
        LogService.Info("Sprache geändert für {0}: {1}", user.Username, language);
    }

    public Task<List<User>> GetUsersAsync() => _storage.LoadUsersAsync();

    /// <summary>Legt einen Benutzer an (Passwort wird als BCrypt-Hash gespeichert, nie Klartext).</summary>
    public async Task CreateUserAsync(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new InvalidOperationException("Benutzername darf nicht leer sein.");

        // Eltern sind automatisch Admin; Kinder haben kein Anmeldekonto (kein Passwort).
        if (user.Category == PersonCategory.Parent) user.Role = UserRole.Admin;
        var needsPassword = user.Category != PersonCategory.Child;
        if (needsPassword && string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Kennwort darf nicht leer sein.");

        var users = await _storage.LoadUsersAsync();
        if (users.Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Benutzer '{user.Username}' existiert bereits.");

        user.PasswordHash = needsPassword && !string.IsNullOrEmpty(password)
            ? BCrypt.Net.BCrypt.HashPassword(password)
            : "";
        if (string.IsNullOrWhiteSpace(user.DisplayName)) user.DisplayName = user.Username;
        users.Add(user);
        await _storage.SaveUsersAsync(users);
        LogService.Info("Benutzer angelegt: {0} (Rolle: {1}, Typ: {2})", user.Username, user.Role, user.Category);
    }

    /// <summary>Aktualisiert Stammdaten (ohne Passwort). Wahrt eindeutigen Usernamen.</summary>
    public async Task UpdateUserAsync(User user)
    {
        var users = await _storage.LoadUsersAsync();
        var existing = users.FirstOrDefault(u => u.Id == user.Id)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        if (users.Any(u => u.Id != user.Id &&
            u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Benutzer '{user.Username}' existiert bereits.");

        // Eltern sind automatisch Admin
        if (user.Category == PersonCategory.Parent) user.Role = UserRole.Admin;

        // Letzten Admin nicht zum Nicht-Admin herabstufen
        if (existing.Role == UserRole.Admin && user.Role != UserRole.Admin &&
            users.Count(u => u.Role == UserRole.Admin) <= 1)
            throw new InvalidOperationException("Es muss mindestens ein Administrator bestehen bleiben.");

        existing.Username = user.Username;
        existing.DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        existing.Role = user.Role;
        existing.Category = user.Category;
        existing.Language = user.Language;
        existing.Email = user.Email;
        existing.WeeklyHoursQuota = user.WeeklyHoursQuota;
        existing.ThemeVariant = user.ThemeVariant;
        existing.AccentColor = user.AccentColor;

        await _storage.SaveUsersAsync(users);
        LogService.Info("Benutzer aktualisiert: {0}", existing.Username);
    }

    public async Task SetPasswordAsync(string userId, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
            throw new InvalidOperationException("Kennwort darf nicht leer sein.");
        var users = await _storage.LoadUsersAsync();
        var user = users.FirstOrDefault(u => u.Id == userId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _storage.SaveUsersAsync(users);
        LogService.Info("Kennwort geändert für {0}", user.Username);
    }

    public async Task DeleteUserAsync(string userId)
    {
        var users = await _storage.LoadUsersAsync();
        var user = users.FirstOrDefault(u => u.Id == userId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        if (user.Role == UserRole.Admin && users.Count(u => u.Role == UserRole.Admin) <= 1)
            throw new InvalidOperationException("Der letzte Administrator kann nicht gelöscht werden.");

        users.Remove(user);
        await _storage.SaveUsersAsync(users);
        LogService.Info("Benutzer gelöscht: {0}", user.Username);
    }
}
