using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    private readonly IStorageService _storage;

    private readonly NotificationService _notifications;

    private readonly AiService _ai;

    private readonly IMailSender _mailSender;

    private List<User> _allUsers = new();

    private List<ShiftSwapRequest> _swapRequests = new();

    private List<ActivityType> _activityTypes = new();

    private List<RecurringActivity> _recurringActivities = new();

    private IReadOnlyList<Holiday> _weekHolidays = Array.Empty<Holiday>();

    private GermanState _holidayState = GermanState.BY;

    private double _overnightHoursPerDay = 2.0;

    private Dictionary<string, string> _userColors = new();

    public User CurrentUser { get; }

    /// <summary>Aktuell geladene Benutzerliste — für View-Code-Behind, das ohne Storage-Zugriff
    /// Personen ins UI lifern muss (z.B. Hinweis-Dialog).</summary>
    public IReadOnlyList<User> AllUsers => _allUsers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateOnly _weekStart;

    public string WeekLabel
    {
        get
        {
            var kw = ISOWeek.GetWeekOfYear(WeekStart.ToDateTime(TimeOnly.MinValue));
            return $"{Localizer.Instance["Cal_Week"]} {kw:D2} / {WeekStart.Year}";
        }
    }

    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();

    public ObservableCollection<WeeklyHoursViewModel> WeeklyHours { get; } = new();

    /// <summary>Tabellarische Sicht: je Person eine Zeile mit 7 Tageszellen.</summary>
    public ObservableCollection<PersonRowViewModel> Rows { get; } = new();

    public bool IsAdmin => CurrentUser.Role == UserRole.Admin;

    public bool CanSwitchView => EffectiveIsAdmin;

    /// <summary>Eltern dürfen finalisieren (organisatorisches Mitspracherecht), sind aber kein Admin.</summary>
    public bool CanFinalize => EffectiveIsAdmin || CurrentUser.Category == PersonCategory.Parent;

    /// <summary>
    /// Admin-only „View-as": Wenn gesetzt, rendert der Kalender alles aus der Perspektive dieses
    /// Users (Privatsphäre-Maskierung wie bei nicht-Admin). Admin-Aktionen (Bearbeiten, Hinzufügen)
    /// sind im View-as-Modus deaktiviert — der Admin schaut nur, was die Person sehen würde.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImpersonating), nameof(ViewAsBanner), nameof(ViewAsUserColor),
        nameof(EffectiveUserId), nameof(EffectiveIsAdmin),
        nameof(CanSwitchView), nameof(CanFinalize))]
    private string? _viewAsUserId;

    public bool IsImpersonating => ViewAsUserId is not null;

    public string EffectiveUserId => ViewAsUserId ?? CurrentUser.Id;

    public bool EffectiveIsAdmin => IsAdmin && ViewAsUserId is null;

    public string ViewAsBanner
    {
        get
        {
            if (ViewAsUserId is null) return "";
            var u = _allUsers.FirstOrDefault(x => x.Id == ViewAsUserId);
            var name = u is null ? ViewAsUserId
                : string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            return string.Format(Localizer.Instance["Cal_ViewAsBanner"], name);
        }
    }

    /// <summary>Farbe der beobachteten Person (Hex) — färbt den View-As-Banner ein,
    /// damit der Admin auf einen Blick sieht, aus wessen Sicht er gerade schaut.</summary>
    public string ViewAsUserColor
    {
        get
        {
            if (ViewAsUserId is null) return "#E67E22";  // Fallback-Orange, wird eh unsichtbar
            var u = _allUsers.FirstOrDefault(x => x.Id == ViewAsUserId);
            return string.IsNullOrEmpty(u?.Color) ? "#7F8C8D" : u!.Color;
        }
    }

    partial void OnViewAsUserIdChanged(string? value)
    {
        LogService.UserAction(CurrentUser.Username,
            value is null ? "View-as beendet" : $"View-as gestartet ({value})");
        _ = LoadWeekAsync();
    }

    [RelayCommand]
    private void ToggleImpersonation(string? userId)
    {
        if (!IsAdmin || string.IsNullOrEmpty(userId)) return;
        // Erneuter Klick auf dieselbe Person beendet View-as.
        ViewAsUserId = ViewAsUserId == userId ? null : userId;
    }

    [RelayCommand]
    private void ExitImpersonation() => ViewAsUserId = null;

    [ObservableProperty] private bool _isHoursPanelVisible;

    /// <summary>true = Normalsicht (eigene hervorgehoben); false = Planungssicht (alle gleich, editierbar).</summary>
    [ObservableProperty] private bool _isPersonalView;

    /// <summary>Woche abgeschlossen (alle 7 Tage finalisiert) → Planen/Urlaub gesperrt (Krank bleibt möglich).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FinalizeButtonKey))]
    private bool _isWeekFinalized;

    public string FinalizeButtonKey => IsWeekFinalized ? "Cal_UnfinalizeWeek" : "Cal_FinalizeWeek";

    /// <summary>Feiertage im Kalender anzeigen (pro Benutzer gemerkt, per Header-Toggle umschaltbar).</summary>
    [ObservableProperty] private bool _isHolidaysVisible = true;

    /// <summary>date, existing (null=neu), users, canPickUser, allowedTypes, activityTypes. Vom CalendarView-Code-Behind abonniert.</summary>
    public event Action<DateOnly, CalendarEntry?, IReadOnlyList<User>, bool, IReadOnlyList<EntryType>, IReadOnlyList<ActivityType>>? EntryDialogRequested;

    /// <summary>Öffnet den Schichttausch-Dialog mit vorbereitetem ViewModel. Vom CalendarView-Code-Behind abonniert.</summary>
    public event Action<ShiftSwapViewModel>? SwapDialogRequested;

    /// <summary>Öffnet den Umplanungs-Dialog (Krankmeldung) mit vorbereitetem ViewModel.</summary>
    public event Action<ReplanViewModel>? ReplanDialogRequested;

    /// <summary>Öffnet den Tages-Hinweis-Dialog (Admin). Parameter: Datum + aktuelle Notiz.</summary>
    public event Action<DateOnly, string, string?>? DayNoteDialogRequested;

    /// <summary>Bittet das CalendarView-Code-Behind, einen Speichern-Dialog für den PDF-Export zu öffnen.</summary>
    public event Action? ExportPdfRequested;

    /// <summary>Öffnet den Empfänger-Dialog für den Plan-Mailversand (vom Code-Behind abonniert).</summary>
    public event Action<MailViewModel>? MailDialogRequested;

    private static readonly IReadOnlyList<EntryType> AllTypes = Enum.GetValues<EntryType>();

    // Selbst-Antrag: Urlaub nur wenn nicht finalisiert, Krank immer.
    private static IReadOnlyList<EntryType> AbsenceTypes(bool finalized) =>
        finalized ? new[] { EntryType.SickLeave } : new[] { EntryType.SickLeave, EntryType.Vacation };

    public CalendarViewModel(IStorageService storage, User user, NotificationService notifications, AiService ai, IMailSender mailSender)
    {
        _storage = storage;
        _notifications = notifications;
        _ai = ai;
        _mailSender = mailSender;
        CurrentUser = user;
        // Admin startet in der Planungssicht; alle anderen fest in der Normalsicht
        _isPersonalView = user.Role != UserRole.Admin;
        _isHolidaysVisible = user.ShowHolidays;
        _weekStart = GetMondayOfWeek(DateOnly.FromDateTime(DateTime.Today));
        RebuildDays();
        _ = LoadAsync();
        Localizer.Instance.LanguageChanged += OnLanguageChanged;
    }

    partial void OnIsPersonalViewChanged(bool value)
    {
        LogService.UserAction(CurrentUser.Username,
            value ? "Ansicht: Normalsicht (eigene Schichten)" : "Ansicht: Planungssicht");
        RebuildDays();          // CanAddEntry je Tag neu berechnen
        _ = LoadWeekAsync();    // Einträge neu auflösen (Hervorhebung/Deckkraft)
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Wochentagsnamen/Eintragstyp-Labels neu erzeugen und Woche neu laden
        RebuildDays();
        OnPropertyChanged(nameof(WeekLabel));
        _ = LoadWeekAsync();
    }

    /// <summary>Vom MainWindowViewModel beim Abmelden/Benutzerwechsel aufrufen (kein Event-Leak).</summary>
    public void Cleanup() => Localizer.Instance.LanguageChanged -= OnLanguageChanged;

    [RelayCommand]
    private async Task CopyWeekToNextAsync()
    {
        if (!IsAdmin) return;
        var copied = 0;
        for (int i = 0; i < 7; i++)
        {
            var src = await _storage.LoadDayAsync(WeekStart.AddDays(i));
            var templates = WeekCopy.TemplateEntries(src.Entries);
            if (templates.Count == 0) continue;

            var dst = await _storage.LoadDayAsync(WeekStart.AddDays(i + 7));
            if (dst.IsFinalized) continue;                                  // finalisierte Tage nicht überschreiben
            if (dst.Entries.Any(e => WeekCopy.IsTemplate(e.Type))) continue; // schon geplant → kein Duplikat

            dst.Entries.AddRange(templates);
            dst.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            await _storage.SaveDayAsync(dst);
            copied++;
        }

        LogService.UserAction(CurrentUser.Username, $"Woche kopiert ({WeekLabel}) → {copied} Tag(e)");

        // Zur nächsten Woche springen, damit das Ergebnis sichtbar ist
        WeekStart = WeekStart.AddDays(7);
        RebuildDays();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task ToggleFinalizeWeekAsync()
    {
        if (!CanFinalize) return;
        var target = !IsWeekFinalized;
        for (int i = 0; i < 7; i++)
        {
            var day = await _storage.LoadDayAsync(WeekStart.AddDays(i));
            day.IsFinalized = target;
            await _storage.SaveDayAsync(day);
        }
        LogService.UserAction(CurrentUser.Username,
            target ? $"Woche finalisiert: {WeekLabel}" : $"Finalisierung aufgehoben: {WeekLabel}");

        if (target)
        {
            // Mitarbeiter mit Arbeitsschichten in der Woche benachrichtigen (außer dem Admin selbst)
            var affected = Days.SelectMany(d => d.Entries)
                .Where(e => e.Type == EntryType.Work && e.UserId != CurrentUser.Id)
                .Select(e => e.UserId);
            var kw = ISOWeek.GetWeekOfYear(WeekStart.ToDateTime(TimeOnly.MinValue));
            await _notifications.AddManyAsync(affected, "Notif_WeekFinalized",
                WeekStart.ToString("yyyy-MM-dd"), kw.ToString("D2"), WeekStart.Year.ToString());
        }

        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        LogService.UserAction(CurrentUser.Username, $"Navigation zurück von {WeekLabel}");
        WeekStart = WeekStart.AddDays(-7);
        RebuildDays();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        LogService.UserAction(CurrentUser.Username, $"Navigation vor von {WeekLabel}");
        WeekStart = WeekStart.AddDays(7);
        RebuildDays();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        LogService.UserAction(CurrentUser.Username, "Navigation zur aktuellen Woche");
        WeekStart = GetMondayOfWeek(DateOnly.FromDateTime(DateTime.Today));
        RebuildDays();
        await LoadWeekAsync();
    }

    /// <summary>Navigiert zu der Woche, die das angegebene Datum enthält (für „zur Woche springen").</summary>
    public async Task GoToWeekContaining(DateOnly date)
    {
        WeekStart = GetMondayOfWeek(date);
        RebuildDays();
        await LoadWeekAsync();
    }

    private async Task LoadAsync()
    {
        _allUsers = await _storage.LoadUsersAsync();
        RebuildUserColors();
        var settings = await _storage.LoadSettingsAsync();
        _holidayState = GermanStates.Parse(settings.HolidayState);
        _overnightHoursPerDay = settings.OvernightHoursPerDay;
        await LoadWeekAsync();
    }

    /// <summary>Nach dem Admin-Bereich oder dem 30s-Hintergrund-Sync: Benutzer, Einstellungen,
    /// Kategorien/Regeln neu laden. <paramref name="silent"/>=true unterdrückt das „Lade
    /// Kalenderwoche"-Statuslog, damit Background-Polls die Statusleiste nicht zuflattern.</summary>
    public async Task RefreshAllAsync(bool silent = false)
    {
        _allUsers = await _storage.LoadUsersAsync();
        RebuildUserColors();
        var settings = await _storage.LoadSettingsAsync();
        _holidayState = GermanStates.Parse(settings.HolidayState);
        _overnightHoursPerDay = settings.OvernightHoursPerDay;
        await LoadWeekAsync(silent);
    }

    /// <summary>Header-Toggle: Feiertags-Anzeige sofort umschalten und die Präferenz pro Benutzer merken.</summary>
    partial void OnIsHolidaysVisibleChanged(bool value)
    {
        foreach (var d in Days) d.SetHolidayVisible(value);
        CurrentUser.ShowHolidays = value;
        _ = PersistShowHolidaysAsync(value);
    }

    private async Task PersistShowHolidaysAsync(bool value)
    {
        var users = await _storage.LoadUsersAsync();
        var u = users.FirstOrDefault(x => x.Id == CurrentUser.Id);
        if (u != null) { u.ShowHolidays = value; await _storage.SaveUsersAsync(users); }
    }
}
