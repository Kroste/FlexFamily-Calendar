namespace FlexFamilyCalendar.DesignApi;

/// <summary>
/// Sperrliste für die Design-Test-API.
///
/// <para>Bei einer reinen Anzeige-App wäre eine Steuer-Schnittstelle harmlos. FlexFamily ist
/// das nicht: im Server-Modus hängt sie an der Live-Datenbank unter flexfamily.cloud, sie
/// verschickt Mails an die ganze Familie und kann sich selbst über ein Update austauschen.
/// Ein einziger falscher Klick über die API richtet echten Schaden an, den niemand mit einem
/// Reload rückgängig macht.</para>
///
/// <para><b>Abweichung vom Skill-Muster mit Absicht:</b> das Vorbild (DTM) sperrt eine Liste
/// bekannter Namen und lässt alles andere durch. Hier ist es umgekehrt — unbekannte Namen sind
/// gesperrt. Eine handgepflegte Liste veraltet garantiert, und der Preis für einen vergessenen
/// Eintrag ist hier nicht ein blockierter Testklick, sondern ein gelöschter Kalendertag in der
/// echten Familien-DB. Der Test in <c>DestructiveGuardTests</c> erzwingt, dass jeder Command
/// ausdrücklich in genau einer der beiden Listen steht, damit die Voreinstellung nicht zur
/// stillen Ausrede wird.</para>
/// </summary>
public static class DestructiveGuard
{
    /// <summary>
    /// Ausdrücklich unbedenklich: öffnet nur Ansichten oder ändert Darstellung. Nichts hiervon
    /// schreibt in den Speicher, verschickt etwas oder verlässt den Prozess.
    /// </summary>
    public static readonly IReadOnlySet<string> Safe = new HashSet<string>(StringComparer.Ordinal)
    {
        // Navigation und Dialoge öffnen
        "OpenInfo", "OpenProfile", "OpenAdmin", "OpenNotifications",
        "OpenMonthOverview", "OpenHoursAccount", "OpenGitHub", "OpenCoffee",
        "Close", "Later", "Skip", "ExitImpersonation", "ToggleImpersonation",
        // Reine Ansichtsschalter
        "ToggleHoursPanel", "PreviousWeek", "NextWeek", "GoToToday",
        // Lesende Prüfung
        "CheckForUpdates",
        // Element-Namen der Kopfleiste und Wochenleiste: /click spricht Controls über ihren
        // x:Name an, nicht über den Command-Namen. Alle hier öffnen nur Ansichten oder
        // schalten die Darstellung um.
        "AdminButton", "MonthOverviewButton", "HoursAccountButton", "ProfileButton",
        "NotificationsButton", "InfoButton",
        "PreviousWeekButton", "NextWeekButton", "TodayButton",
        "PersonalViewToggle", "HoursToggleButton", "HolidaysToggle",
        "LanguageBox", "ConnectionButton",
    };

    /// <summary>
    /// Gesperrt, weil es Daten schreibt, nach außen kommuniziert oder den Prozess ersetzt.
    /// Die Namen decken Commands wie Element-Namen ab — es reicht nicht, den Command zu
    /// sperren und den Bestätigen-Knopf offen zu lassen.
    /// </summary>
    public static readonly IReadOnlySet<string> Blocked = new HashSet<string>(StringComparer.Ordinal)
    {
        // Schreibt in Kalender/Storage
        "Save", "SaveCommand", "Delete", "DeleteEntry", "DeleteUser", "Remove",
        "CopyWeekToNext", "ToggleFinalizeWeek", "ApplyEntryResult", "AddForCell",
        "ReorderPerson", "HandleEntryDrop", "ApplyDayNote", "ApplyReplanResult",
        // Verlässt den Rechner
        "MailPlan", "SendPlanMail", "ExportPdf",
        // Schichttausch verändert fremde Pläne
        "RequestInitiateSwap", "RespondToSwap", "WithdrawSwap", "AcceptSwap", "ApplySwapResult",
        // KI schickt Plandaten an einen Provider
        "OpenAiPlanner", "ApplyAiSuggestion",
        // Ersetzt die laufende Anwendung
        "Install", "RunUpdate",
        // Beendet die Sitzung, damit fällt die API-Gegenprobe in sich zusammen
        "Logout",
        // Bestätigungs-Schaltflächen: sonst klickt man die Sperre einfach weg
        "OkButton", "ConfirmButton", "SaveButton", "DeleteButton", "ApplyButton", "SendButton",
        // Element-Namen schreibender Aktionen
        "LogoutButton", "ExportPdfButton", "MailButton", "AiPlannerButton",
        "CopyWeekButton", "FinalizeButton", "SignInButton", "CreateAdminButton",
    };

    /// <summary>
    /// <c>true</c>, wenn der Name über die API ausgelöst werden darf. Alles, was in keiner der
    /// beiden Listen steht, gilt als gesperrt.
    /// </summary>
    public static bool IsAllowed(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && !Blocked.Contains(name)
           && Safe.Contains(name);

    /// <summary>Begründung für die 403-Antwort — sagt, welcher der beiden Fälle vorliegt.</summary>
    public static string ReasonFor(string? name)
        => string.IsNullOrWhiteSpace(name) ? "kein Elementname angegeben"
         : Blocked.Contains(name) ? "ausdrücklich gesperrt (verändert Daten oder kommuniziert nach außen)"
         : "nicht als unbedenklich gelistet — Voreinstellung ist gesperrt";
}
