using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Services;

public class AuthService
{
    private readonly IStorageService _storage;
    private readonly ApiClient? _api;   // gesetzt = Server-Modus (Anmeldung gegen die API)

    public AuthService(IStorageService storage, ApiClient? api = null)
    {
        _storage = storage;
        _api = api;
    }

    public bool IsServerMode => _api is not null;

    public async Task<User?> LoginAsync(string username, string password)
    {
        if (_api is not null)
        {
            LogService.Debug("Server-Anmeldeversuch für Benutzer: {0}", username);
            var login = await _api.LoginAsync(username, password);
            if (login is null)
            {
                LogService.Warn("Server-Anmeldung fehlgeschlagen für '{0}'", username);
                return null;
            }
            LogService.Info("Server-Anmeldung erfolgreich: {0} (Rolle: {1})", login.User.Username, login.User.Role);
            return UserMapping.ToDesktop(login.User);
        }

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
        => _api is not null || (await _storage.LoadUsersAsync()).Count > 0;   // Server hat immer den Erst-Admin

    /// <summary>Merkt den Benutzernamen für Auto-Login (null/leer = Merker löschen). Kein Passwort.</summary>
    public async Task SetRememberedUsernameAsync(string? username)
    {
        var settings = await _storage.LoadSettingsAsync();
        settings.RememberedUsername = username ?? "";

        // Im Server-Modus zusätzlich das JWT (kein Passwort!) verschlüsselt merken bzw. löschen.
        if (_api is not null)
            settings.ServerTokenEnc = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(_api.Token)
                ? SecretService.Encrypt(_api.Token!)
                : "";

        await _storage.SaveSettingsAsync(settings);
        LogService.Info(string.IsNullOrEmpty(settings.RememberedUsername)
            ? "Login merken deaktiviert"
            : $"Login merken aktiviert für {settings.RememberedUsername}");
    }

    /// <summary>Liefert den gemerkten Benutzer oder null. Leert den Merker, falls der Benutzer fehlt.</summary>
    public async Task<User?> GetRememberedUserAsync()
    {
        // Im Server-Modus: gemerktes JWT wiederverwenden (kein Passwort gespeichert).
        if (_api is not null)
        {
            var s = await _storage.LoadSettingsAsync();
            if (string.IsNullOrEmpty(s.RememberedUsername) || string.IsNullOrEmpty(s.ServerTokenEnc))
                return null;
            try
            {
                _api.SetToken(SecretService.Decrypt(s.ServerTokenEnc));
                var me = await _api.GetMeAsync();
                if (me is not null)
                {
                    LogService.Info("Auto-Login (Server) per gemerktem Token: {0}", me.Username);
                    return UserMapping.ToDesktop(me);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("Auto-Login (Server) fehlgeschlagen: {0}", ex.Message);
            }
            // Token ungültig/abgelaufen → Merker leeren, normaler Login folgt.
            s.ServerTokenEnc = "";
            s.RememberedUsername = "";
            await _storage.SaveSettingsAsync(s);
            return null;
        }

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
        if (_api is not null)
        {
            // Self-Profil-Änderung; eigener Endpunkt fehlt noch (Admin-PUT würde Nicht-Admins 403 geben).
            LogService.Debug("Sprachänderung im Server-Modus noch nicht persistiert (Self-Profil-Endpunkt folgt): {0}", language);
            return;
        }

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

        if (string.IsNullOrWhiteSpace(user.DisplayName)) user.DisplayName = user.Username;
        if (string.IsNullOrWhiteSpace(user.Color)) user.Color = UserColorPalette.ColorAt(users.Count);
        if (user.AccountStart == default)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            user.AccountStart = new DateOnly(today.Year, today.Month, 1);
        }

        if (_api is not null)
        {
            // Server hasht das Passwort; der Klartext steht nur im Body (wird nicht geloggt).
            await _api.CreateUserAsync(UserMapping.ToCreateBody(user, needsPassword ? password : ""));
            LogService.Info("Benutzer angelegt (Server): {0} (Rolle: {1}, Typ: {2})", user.Username, user.Role, user.Category);
            return;
        }

        user.PasswordHash = needsPassword && !string.IsNullOrEmpty(password)
            ? BCrypt.Net.BCrypt.HashPassword(password)
            : "";
        users.Add(user);
        await _storage.SaveUsersAsync(users);
        LogService.Info("Benutzer angelegt: {0} (Rolle: {1}, Typ: {2})", user.Username, user.Role, user.Category);
    }

    /// <summary>Aktualisiert Stammdaten (ohne Passwort). Wahrt eindeutigen Usernamen.</summary>
    public async Task UpdateUserAsync(User user)
    {
        if (user.Category == PersonCategory.Parent) user.Role = UserRole.Admin;

        if (_api is not null)
        {
            // Server erzwingt Eindeutigkeit + Letzter-Admin-Schutz und liefert die Fehlermeldung.
            await _api.UpdateUserAsync(user.Id, UserMapping.ToUpdateBody(user));
            LogService.Info("Benutzer aktualisiert (Server): {0}", user.Username);
            return;
        }

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
        existing.MaxWeeklyHours = user.MaxWeeklyHours;
        existing.MaxDailyHours = user.MaxDailyHours;
        existing.MinRestHours = user.MinRestHours;
        existing.ThemeVariant = user.ThemeVariant;
        existing.Color = user.Color;
        existing.OpeningBalanceHours = user.OpeningBalanceHours;
        existing.AccountStart = user.AccountStart;
        existing.ShowHolidays = user.ShowHolidays;

        await _storage.SaveUsersAsync(users);
        LogService.Info("Benutzer aktualisiert: {0}", existing.Username);
    }

    public async Task SetPasswordAsync(string userId, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
            throw new InvalidOperationException("Kennwort darf nicht leer sein.");

        if (_api is not null)
        {
            await _api.SetUserPasswordAsync(userId, newPassword);
            LogService.Info("Kennwort geändert (Server): id={0}", userId);
            return;
        }

        var users = await _storage.LoadUsersAsync();
        var user = users.FirstOrDefault(u => u.Id == userId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _storage.SaveUsersAsync(users);
        LogService.Info("Kennwort geändert für {0}", user.Username);
    }

    public async Task DeleteUserAsync(string userId)
    {
        if (_api is not null)
        {
            await _api.DeleteUserAsync(userId);   // Server prüft den Letzter-Admin-Schutz
            LogService.Info("Benutzer gelöscht (Server): id={0}", userId);
            return;
        }

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
