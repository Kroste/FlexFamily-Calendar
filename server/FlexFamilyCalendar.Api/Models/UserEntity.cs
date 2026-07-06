namespace FlexFamilyCalendar.Api.Models;

/// <summary>Serverseitige Benutzer-Entität (saubere Persistenz; keine UI-Laufzeitfelder).</summary>
public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";   // BCrypt — kompatibel zur Desktop-App
    public string Role { get; set; } = "User";         // "Admin" | "User"
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Category { get; set; } = "Employee"; // Parent | Child | Employee | AuPair
    public double WeeklyHoursQuota { get; set; }
    public double MaxWeeklyHours { get; set; }
    public double MaxDailyHours { get; set; }
    public double MinRestHours { get; set; }
    public string Color { get; set; } = "";
    public string Language { get; set; } = "de";
    public string AiStyleHint { get; set; } = "";       // Freitext-Stil-Wunsch für die KI
    public double OpeningBalanceHours { get; set; }      // Stundenkonto: Anfangssaldo (Übertrag von vorher)
    public DateOnly AccountStart { get; set; }           // Stundenkonto: Start (leer = bei Anlage gesetzt)
    public string ThemeVariant { get; set; } = "System"; // System | Light | Dark (UI-Präferenz)
    public bool ShowHolidays { get; set; } = true;       // Anzeige-Präferenz: Feiertage einblenden
}
