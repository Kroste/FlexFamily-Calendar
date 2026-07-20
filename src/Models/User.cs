namespace FlexFamilyCalendar.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";  // BCrypt hash — never plaintext
    public UserRole Role { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Language { get; set; } = "de";  // UI-Sprache des Benutzers
    public PersonCategory Category { get; set; } = PersonCategory.Employee;
    public double WeeklyHoursQuota { get; set; }  // Wochenstunden-Soll (0 = kein Soll)
    public double MaxWeeklyHours { get; set; }    // Gesetzliche Wochen-Höchstarbeitszeit (0 = kein Limit)
    public double MaxDailyHours { get; set; }     // Gesetzliche Tages-Höchstarbeitszeit (0 = kein Limit)
    public double MinRestHours { get; set; }      // Mindest-Ruhezeit zwischen zwei Tagen (0 = keine Prüfung)
    public string ThemeVariant { get; set; } = "System";  // System | Light | Dark
    public string Color { get; set; } = "";  // Personenfarbe im Kalender (leer = bei Anlage vergeben)
    public double OpeningBalanceHours { get; set; }       // Stundenkonto: Anfangssaldo (Übertrag von vorher)
    public DateOnly AccountStart { get; set; }            // Stundenkonto: Start (leer = bei Anlage gesetzt)
    public bool ShowHolidays { get; set; } = true;        // Anzeige-Präferenz: Feiertage im Kalender einblenden
    /// <summary>Freitext-Stil-Wunsch für die KI (Anrede, Tonfall). Leer = neutrales Default.</summary>
    public string AiStyleHint { get; set; } = "";
    /// <summary>Hinweistexte (Tooltips) im UI anzeigen. Default an, kann im Profil abgeschaltet werden.</summary>
    public bool ShowHints { get; set; } = true;
    /// <summary>Ist die First-Run-Onboarding-Tour bereits durchgelaufen?</summary>
    public bool OnboardingSeen { get; set; }
    /// <summary>Anzeige-Position in der Personen-Sortierung der Planansicht (kleinere Zahl = weiter oben).</summary>
    public int PlanOrder { get; set; } = 100;
}
