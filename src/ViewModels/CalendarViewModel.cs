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
    private readonly StorageService _storage;
    private readonly NotificationService _notifications;
    private readonly AiService _ai;
    private List<User> _allUsers = new();
    private List<ShiftSwapRequest> _swapRequests = new();
    private List<ActivityType> _activityTypes = new();
    private List<RecurringActivity> _recurringActivities = new();
    private IReadOnlyList<Holiday> _weekHolidays = Array.Empty<Holiday>();
    private GermanState _holidayState = GermanState.BY;
    private double _overnightHoursPerDay = 2.0;
    private Dictionary<string, string> _userColors = new();

    public User CurrentUser { get; }

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
    public bool CanSwitchView => IsAdmin;

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
    public event Action<DateOnly, string>? DayNoteDialogRequested;

    /// <summary>Bittet das CalendarView-Code-Behind, einen Speichern-Dialog für den PDF-Export zu öffnen.</summary>
    public event Action? ExportPdfRequested;

    /// <summary>Öffnet den Empfänger-Dialog für den Plan-Mailversand (vom Code-Behind abonniert).</summary>
    public event Action<MailViewModel>? MailDialogRequested;

    private static readonly IReadOnlyList<EntryType> AllTypes = Enum.GetValues<EntryType>();

    // Selbst-Antrag: Urlaub nur wenn nicht finalisiert, Krank immer.
    private static IReadOnlyList<EntryType> AbsenceTypes(bool finalized) =>
        finalized ? new[] { EntryType.SickLeave } : new[] { EntryType.SickLeave, EntryType.Vacation };

    public CalendarViewModel(StorageService storage, User user, NotificationService notifications, AiService ai)
    {
        _storage = storage;
        _notifications = notifications;
        _ai = ai;
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
        if (!IsAdmin) return;
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
    private void ToggleHoursPanel()
    {
        IsHoursPanelVisible = !IsHoursPanelVisible;
        if (IsHoursPanelVisible) RecomputeWeeklyHours();
    }

    [RelayCommand]
    private void ExportPdf()
    {
        LogService.Click(CurrentUser.Username, $"PDF-Export ({WeekLabel})");
        ExportPdfRequested?.Invoke();
    }

    /// <summary>Vorgeschlagener Dateiname für den PDF-Export der aktuellen Woche.</summary>
    public string ExportFileName
    {
        get
        {
            var kw = ISOWeek.GetWeekOfYear(WeekStart.ToDateTime(TimeOnly.MinValue));
            return $"Plan_KW{kw:D2}_{WeekStart.Year}.pdf";
        }
    }

    /// <summary>Export-Modell aus Sicht des aktuellen Benutzers (für den „PDF"-Button).</summary>
    public WeekExport CreateWeekExport() => CreateWeekExport(CurrentUser);

    /// <summary>Baut das Export-Modell als Tabelle (Person × Wochentag) aus Sicht von <paramref name="viewer"/> (Datenschutz-Maskierung).</summary>
    public WeekExport CreateWeekExport(User viewer)
    {
        string TypeLabel(EntryType t) => Localizer.Instance[EntryTypeInfo.Key(t)];
        var isAdmin = viewer.Role == UserRole.Admin;

        var headers = Days.Select(d => new PlanDayHeader(d.DayName, d.DateLabel, d.HolidayName)).ToList();
        var notes = Days.Select(d => d.DayNote ?? "").ToList();

        var rows = new List<PlanPersonRow>();
        foreach (var r in Rows)
        {
            var cells = r.Cells
                .Select(c => (IReadOnlyList<PlanCellEntry>)c.Entries
                    .Select(e => PlanExportBuilder.CellEntry(e, isAdmin, viewer.Id, TypeLabel)).ToList())
                .ToList();
            rows.Add(new PlanPersonRow(r.Name, r.Color, r.CategoryLabel, cells));
        }

        var generated = string.Format(Localizer.Instance["Pdf_Generated"],
            DateTime.Now.ToString("g", CultureInfo.CurrentCulture));
        return new WeekExport(Localizer.Instance["Pdf_Title"], WeekLabel, generated, headers, rows, notes);
    }

    /// <summary>Plan per E-Mail senden: prüft die SMTP-Konfiguration und öffnet die Empfänger-Auswahl (nur Admin).</summary>
    [RelayCommand]
    private async Task MailPlan()
    {
        if (!IsAdmin) return;
        var settings = await _storage.LoadSettingsAsync();
        if (!MailComposer.IsConfigured(settings)) { LogService.Warn(Localizer.Instance["Mail_NotConfigured"]); return; }
        var recipients = MailComposer.RecipientsWithEmail(_allUsers);
        if (recipients.Count == 0) { LogService.Warn(Localizer.Instance["Mail_NoRecipients"]); return; }

        LogService.Click(CurrentUser.Username, $"Mail-Versand ({WeekLabel})");
        MailDialogRequested?.Invoke(new MailViewModel(recipients));
    }

    /// <summary>Sendet jedem Empfänger ein eigenes, aus seiner Sicht maskiertes Wochen-PDF (einzelne Mails).</summary>
    public async Task SendPlanMailAsync(IReadOnlyList<string> emails)
    {
        if (emails.Count == 0) return;
        var settings = await _storage.LoadSettingsAsync();
        var subject = $"{Localizer.Instance["Pdf_Title"]} {WeekLabel}";
        var body = string.Format(Localizer.Instance["Mail_Body"], WeekLabel);

        var sent = 0;
        foreach (var email in emails)
        {
            var viewer = _allUsers.FirstOrDefault(u =>
                u.Email.Trim().Equals(email, StringComparison.OrdinalIgnoreCase));
            if (viewer == null) continue;
            try
            {
                var pdf = PdfExportService.Render(CreateWeekExport(viewer));   // aus Sicht des Empfängers
                await MailService.SendAsync(settings, email, subject, body, pdf, ExportFileName);
                sent++;
            }
            catch (Exception ex)
            {
                LogService.Error("Mail-Versand an einen Empfänger fehlgeschlagen", ex);
            }
        }
        LogService.Info(string.Format(Localizer.Instance["Mail_Sent"], sent));
    }

    /// <summary>Ist-Stunden je Person (Work+Au-Pair) der Woche; nur Personen mit Soll&gt;0.</summary>
    private void RecomputeWeeklyHours()
    {
        var entries = Days.SelectMany(d => d.Entries).Where(e => !e.IsRecurring).ToList();
        var actualByUser = WeeklyHoursCalculator.ActualHoursByUser(entries, _overnightHoursPerDay);
        var workedByUser = WeeklyHoursCalculator.WorkedHoursByUser(entries);
        var daysOrdered = Days.OrderBy(d => d.Date).ToList();

        WeeklyHours.Clear();
        var people = WeeklyHoursCalculator.RelevantUsers(_allUsers, CurrentUser, IsPersonalView);
        foreach (var u in people.OrderBy(u => u.DisplayName))
        {
            var actual = actualByUser.GetValueOrDefault(u.Id);
            var worked = workedByUser.GetValueOrDefault(u.Id);
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            var warnings = DailyAndRestWarnings(u, daysOrdered);
            WeeklyHours.Add(new WeeklyHoursViewModel(name, actual, u.WeeklyHoursQuota, worked, u.MaxWeeklyHours, warnings));
        }
    }

    /// <summary>Tages-Höchstarbeitszeit- und Ruhezeit-Warnungen für einen Benutzer über die sichtbare Woche.</summary>
    private static IReadOnlyList<string> DailyAndRestWarnings(User u, IReadOnlyList<CalendarDayViewModel> daysOrdered)
    {
        var summaries = daysOrdered
            .Select(d => WorkTimeRules.Summarize(d.Date, d.Entries.Where(e => e.UserId == u.Id && !e.IsRecurring)))
            .ToList();

        var warnings = new List<string>();
        var dayFmt = "ddd dd.MM.";

        foreach (var day in WorkTimeRules.OverDailyLimit(summaries, u.MaxDailyHours))
            warnings.Add($"⚠ {Localizer.Instance["Cal_OverDailyLimit"]} ({day.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}): " +
                         $"{H(day.WorkedHours)} / {H(u.MaxDailyHours)} h");

        foreach (var (prev, next, restHours) in WorkTimeRules.ShortRests(summaries, u.MinRestHours))
            warnings.Add($"⚠ {Localizer.Instance["Cal_ShortRest"]} ({prev.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}→" +
                         $"{next.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}): {H(restHours)} / {H(u.MinRestHours)} h");

        foreach (var day in daysOrdered)
            foreach (var (first, second) in WorkTimeRules.WorkOverlaps(day.Entries.Where(e => e.UserId == u.Id && !e.IsRecurring)))
                warnings.Add($"⚠ {Localizer.Instance["Cal_Overlap"]} ({day.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}): " +
                             $"{first.TimeRange} ↔ {second.TimeRange}");

        return warnings;
    }

    private static string H(double v) => v.ToString("0.#", CultureInfo.CurrentCulture);

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

    public void RequestAddEntry(DateOnly date) => RequestAddEntry(date, null);

    /// <summary>Neuer Eintrag; ist <paramref name="person"/> gesetzt, ist die Person fix (Klick in deren Tabellenzeile).</summary>
    public void RequestAddEntry(DateOnly date, User? person)
    {
        if (person != null)
        {
            EntryDialogRequested?.Invoke(date, null, new List<User> { person }.AsReadOnly(), false, AllTypes, _activityTypes);
            return;
        }
        var users = _allUsers.Count > 0 ? _allUsers : new List<User> { CurrentUser };
        EntryDialogRequested?.Invoke(date, null, users.AsReadOnly(), true, AllTypes, _activityTypes);
    }

    /// <summary>Selbst-Antrag: Benutzer meldet sich krank / trägt Urlaub ein (nur für sich).
    /// Krank ist immer möglich, Urlaub nur wenn die Woche nicht finalisiert ist.</summary>
    public void RequestSelfAbsence(DateOnly date)
    {
        var finalized = Days.FirstOrDefault(d => d.Date == date)?.IsFinalized ?? false;
        LogService.Click(CurrentUser.Username, $"Krank/Urlaub eintragen ({date:dd.MM.yyyy})");
        EntryDialogRequested?.Invoke(date, null, new List<User> { CurrentUser }.AsReadOnly(),
            false, AbsenceTypes(finalized), _activityTypes);
    }

    public void RequestEditEntry(DateOnly date, CalendarEntry entry)
    {
        var finalized = Days.FirstOrDefault(d => d.Date == date)?.IsFinalized ?? false;
        var adminEdit = IsAdmin && !IsPersonalView && !finalized;   // finalisiert = gesperrt (Admin entsperrt zuerst)
        // Eigene Abwesenheit: Krank immer editierbar, Urlaub nur wenn nicht finalisiert
        var ownPrivate = !IsAdmin && entry.UserId == CurrentUser.Id && EntryPrivacy.IsPrivate(entry.Type);
        var selfEdit = ownPrivate && (entry.Type == EntryType.SickLeave || !finalized);
        if (!adminEdit && !selfEdit) return;

        LogService.Click(CurrentUser.Username, $"Eintrag bearbeiten ({date:dd.MM.yyyy}, {entry.TypeLabel})");
        var users = adminEdit
            ? (_allUsers.Count > 0 ? _allUsers : new List<User> { CurrentUser })
            : new List<User> { CurrentUser };
        var allowed = adminEdit ? AllTypes : AbsenceTypes(finalized);
        // Abwesenheit: Editor auf den Beginn des Zeitraums öffnen (für die von-bis-Bearbeitung).
        var editDate = EntryTypeInfo.IsAbsence(entry.Type) && entry.AbsenceStart is { } s ? s : date;
        EntryDialogRequested?.Invoke(editDate, entry, users.AsReadOnly(), adminEdit, allowed, _activityTypes);
    }

    /// <summary>Speichert/löscht das Dialog-Ergebnis: ein Pfad für Neu, Edit und Delete.</summary>
    public async Task ApplyEntryResultAsync(DateOnly date, EntryDialogResult result)
    {
        // Abwesenheiten (Urlaub/Krank/Abwesend) werden als Datumsbereich behandelt.
        if (EntryTypeInfo.IsAbsence(result.Entry.Type))
        {
            await ApplyAbsenceResultAsync(date, result);
            return;
        }

        // Umwandlung Abwesenheit → Arbeit/Aktivität: alte Abwesenheits-Gruppe aufräumen.
        if (!string.IsNullOrEmpty(result.Entry.AbsenceGroupId)
            && result.Entry.AbsenceStart is { } os && result.Entry.AbsenceEnd is { } oe)
            await RemoveAbsenceGroupAsync(result.Entry.AbsenceGroupId!, os, oe);
        result.Entry.AbsenceGroupId = null;
        result.Entry.AbsenceStart = null;
        result.Entry.AbsenceEnd = null;

        var day = await _storage.LoadDayAsync(date);
        day.Entries.RemoveAll(e => e.Id == result.Entry.Id);
        if (result.Action == EntryDialogAction.Save)
            day.Entries.Add(result.Entry);
        day.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        await _storage.SaveDayAsync(day);

        var verb = result.Action == EntryDialogAction.Save ? "gespeichert" : "gelöscht";
        LogService.UserAction(CurrentUser.Username,
            $"Eintrag {verb}: {result.Entry.TypeLabel} für {result.Entry.UserDisplayName} am {date:dd.MM.yyyy}");

        await LoadWeekAsync();
        await NotifyEntryChangeAsync(date, result);
    }

    /// <summary>Speichert/löscht eine Abwesenheit als Datumsbereich: je Tag ein Eintrag, verbunden über die GroupId.</summary>
    private async Task ApplyAbsenceResultAsync(DateOnly originalDate, EntryDialogResult result)
    {
        var e = result.Entry;

        // 1. Ursprünglichen Einzeleintrag entfernen (Ein-Tages-Bearbeitung oder Umwandlung Arbeit→Abwesenheit).
        var origDay = await _storage.LoadDayAsync(originalDate);
        if (origDay.Entries.RemoveAll(x => x.Id == e.Id) > 0)
            await _storage.SaveDayAsync(origDay);

        // 2. Vorhandene Gruppe (beim Bearbeiten/Löschen) über ihren Zeitraum entfernen.
        if (!string.IsNullOrEmpty(e.AbsenceGroupId) && e.AbsenceStart is { } gs && e.AbsenceEnd is { } ge)
            await RemoveAbsenceGroupAsync(e.AbsenceGroupId!, gs, ge);

        if (result.Action == EntryDialogAction.Save)
        {
            var groupId = string.IsNullOrEmpty(e.AbsenceGroupId) ? Guid.NewGuid().ToString() : e.AbsenceGroupId!;
            foreach (var (d, entry) in AbsencePlanner.Build(e, result.RangeStart, result.RangeEnd, groupId))
            {
                var day = await _storage.LoadDayAsync(d);
                if (day.IsFinalized && entry.Type == EntryType.Vacation) continue;  // Urlaub nicht in finalisierte Tage
                day.Entries.Add(entry);
                day.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
                await _storage.SaveDayAsync(day);
            }
            LogService.UserAction(CurrentUser.Username,
                $"Abwesenheit ({e.TypeLabel}) für {e.UserDisplayName}: {result.RangeStart:dd.MM.}–{result.RangeEnd:dd.MM.}");

            // Selbst-Krankmeldung → Admins benachrichtigen (einmal, mit Umplanungs-Einstieg am Startdatum).
            if (!IsAdmin && e.Type == EntryType.SickLeave)
            {
                var admins = _allUsers.Where(u => u.Role == UserRole.Admin).Select(u => u.Id);
                var who = string.IsNullOrEmpty(CurrentUser.DisplayName) ? CurrentUser.Username : CurrentUser.DisplayName;
                await _notifications.AddSickReplanAsync(admins, CurrentUser.Id,
                    result.RangeStart.ToString("yyyy-MM-dd"), who, result.RangeStart.ToString("dd.MM.yyyy"));
            }
        }
        else
        {
            LogService.UserAction(CurrentUser.Username, $"Abwesenheit gelöscht: {e.TypeLabel} für {e.UserDisplayName}");
        }

        await LoadWeekAsync();
    }

    /// <summary>Entfernt alle Tageseinträge einer Abwesenheits-Gruppe über ihren (inklusiven) Zeitraum.</summary>
    private async Task RemoveAbsenceGroupAsync(string groupId, DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var day = await _storage.LoadDayAsync(d);
            if (day.Entries.RemoveAll(x => x.AbsenceGroupId == groupId) > 0)
                await _storage.SaveDayAsync(day);
        }
    }

    /// <summary>Benachrichtigt Betroffene: Admin ändert/entfernt fremde Schicht; Mitarbeiter meldet sich krank → an Admins.</summary>
    private async Task NotifyEntryChangeAsync(DateOnly date, EntryDialogResult result)
    {
        var entry = result.Entry;
        var dateStr = date.ToString("yyyy-MM-dd");
        var dateLabel = date.ToString("dd.MM.yyyy");

        // Admin ändert/entfernt die Schicht eines anderen Benutzers
        if (IsAdmin && entry.UserId != CurrentUser.Id)
        {
            var key = result.Action == EntryDialogAction.Save ? "Notif_ShiftChanged" : "Notif_ShiftRemoved";
            await _notifications.AddAsync(entry.UserId, key, dateStr, dateLabel);
            return;
        }

        // Nicht-Admin meldet sich krank → alle Admins benachrichtigen (mit Umplanungs-Einstieg)
        if (!IsAdmin && result.Action == EntryDialogAction.Save && entry.Type == EntryType.SickLeave)
        {
            var admins = _allUsers.Where(u => u.Role == UserRole.Admin).Select(u => u.Id);
            var who = string.IsNullOrEmpty(CurrentUser.DisplayName) ? CurrentUser.Username : CurrentUser.DisplayName;
            await _notifications.AddSickReplanAsync(admins, CurrentUser.Id, dateStr, who, dateLabel);
        }
    }

    /// <summary>Antippen einer Schicht: offene Anfrage beantworten/zurückziehen, eigenen Tausch anbieten oder bearbeiten.</summary>
    public void ActivateEntry(DateOnly date, CalendarEntry entry)
    {
        // Projektionen/Fortsetzungen sind keine echten Tageseinträge → nicht editier-/tauschbar.
        if (entry.IsRecurring || entry.IsContinuation) return;

        var dayStr = date.ToString("yyyy-MM-dd");
        bool Involves(ShiftSwapRequest r) =>
            (r.FromDate == dayStr && r.FromEntryId == entry.Id)
            || (r.Mode == SwapMode.Exchange && r.ToDate == dayStr && r.ToEntryId == entry.Id);

        var incoming = _swapRequests.FirstOrDefault(r =>
            r.Status == SwapStatus.Pending && r.ToUserId == CurrentUser.Id && Involves(r));
        if (incoming != null) { RespondToSwap(incoming); return; }

        var outgoing = _swapRequests.FirstOrDefault(r =>
            r.Status == SwapStatus.Pending && r.FromUserId == CurrentUser.Id && Involves(r));
        if (outgoing != null) { WithdrawSwap(outgoing); return; }

        // Admin tippt eine Krank-Schicht an → Umplanungs-Vorschlag für die Arbeitsschicht(en) der Person
        if (IsAdmin && entry.Type == EntryType.SickLeave)
        {
            RequestReplan(entry.UserId, date);
            return;
        }

        var finalized = Days.FirstOrDefault(d => d.Date == date)?.IsFinalized ?? false;
        if (!IsAdmin && entry.UserId == CurrentUser.Id && entry.Type == EntryType.Work && !finalized)
        {
            RequestInitiateSwap(date, entry);
            return;
        }

        RequestEditEntry(date, entry);
    }

    /// <summary>Aus der Benachrichtigung: zur betroffenen Woche springen und den Umplanungs-Dialog öffnen.</summary>
    public async Task StartReplanAsync(string absentUserId, DateOnly date)
    {
        await GoToWeekContaining(date);
        RequestReplan(absentUserId, date);
    }

    /// <summary>Öffnet den Krankmeldungs-Dialog: gesund melden und – falls eine offene Arbeitsschicht besteht – umplanen.</summary>
    public void RequestReplan(string absentUserId, DateOnly date)
    {
        var dayVm = Days.FirstOrDefault(d => d.Date == date);
        var hasSick = dayVm?.Entries.Any(e => e.UserId == absentUserId && e.Type == EntryType.SickLeave) ?? false;
        if (!hasSick) { LogService.Warn(Localizer.Instance["Replan_NoSick"]); return; }

        var absentShift = dayVm!.Entries
            .Where(e => e.UserId == absentUserId && e.Type == EntryType.Work)
            .OrderBy(e => e.StartTime)
            .FirstOrDefault();

        var candidates = absentShift != null
            ? ReplanEngine.FindCandidates(absentShift, date, _allUsers,
                absentUserId, Days.Select(d => (d.Date, (IReadOnlyList<CalendarEntry>)d.Entries.ToList())).ToList())
            : Array.Empty<ReplanEngine.ReplanCandidate>();

        var person = _allUsers.FirstOrDefault(u => u.Id == absentUserId);
        var personName = person == null ? absentUserId
            : (string.IsNullOrEmpty(person.DisplayName) ? person.Username : person.DisplayName);

        LogService.Click(CurrentUser.Username, $"Krankmeldung ({date:dd.MM.yyyy})");
        ReplanDialogRequested?.Invoke(new ReplanViewModel(_ai, absentUserId, personName, date, absentShift, candidates));
    }

    /// <summary>Verarbeitet das Dialog-Ergebnis: Krankmeldung aufheben oder Schicht an den Ersatz umbuchen.</summary>
    public async Task ApplyReplanResultAsync(ReplanResult result)
    {
        if (result.Action == ReplanAction.MarkHealthy)
        {
            var day = await _storage.LoadDayAsync(result.Date);
            var removed = day.Entries.RemoveAll(e => e.UserId == result.SickUserId && e.Type == EntryType.SickLeave);
            if (removed > 0) await _storage.SaveDayAsync(day);
            LogService.UserAction(CurrentUser.Username, $"Gesund gemeldet ({result.Date:dd.MM.yyyy})");
            await LoadWeekAsync();
            return;
        }

        // TakeOver: ausgefallene Schicht dem Ersatz zuweisen (auch bei finalisierter Woche)
        var d = await _storage.LoadDayAsync(result.Date);
        var shift = d.Entries.FirstOrDefault(e => e.Id == result.ShiftId);
        if (shift == null || result.Replacement == null) { LogService.Warn(Localizer.Instance["Replan_NoShift"]); return; }

        var name = string.IsNullOrEmpty(result.Replacement.DisplayName)
            ? result.Replacement.Username : result.Replacement.DisplayName;
        shift.UserId = result.Replacement.Id;
        shift.UserDisplayName = name;
        await _storage.SaveDayAsync(d);

        await _notifications.AddAsync(result.Replacement.Id, "Notif_ShiftAssigned",
            result.Date.ToString("yyyy-MM-dd"), result.Date.ToString("dd.MM.yyyy"));
        LogService.UserAction(CurrentUser.Username, $"Schicht umgeplant auf {name} ({result.Date:dd.MM.yyyy})");
        await LoadWeekAsync();
    }

    private void RequestInitiateSwap(DateOnly date, CalendarEntry entry)
    {
        var colleagues = _allUsers
            .Where(u => u.Id != CurrentUser.Id
                && (u.Category == PersonCategory.Employee || u.Category == PersonCategory.AuPair))
            .ToList();
        if (colleagues.Count == 0) { LogService.Warn(Localizer.Instance["Swap_NoColleagues"]); return; }

        var colleagueIds = colleagues.Select(c => c.Id).ToHashSet();
        var shifts = new List<SwapShiftOption>();
        foreach (var d in Days.Where(d => !d.IsFinalized))
            foreach (var e in d.Entries)
                if (e.Type == EntryType.Work && e.Id != entry.Id && colleagueIds.Contains(e.UserId))
                    shifts.Add(new SwapShiftOption(e.Id, d.Date.ToString("yyyy-MM-dd"), e.UserId,
                        $"{d.Date.ToString("ddd dd.MM.", CultureInfo.CurrentCulture)} {e.TimeRange}"));

        LogService.Click(CurrentUser.Username, $"Tausch anbieten ({date:dd.MM.yyyy})");
        SwapDialogRequested?.Invoke(new ShiftSwapViewModel(CurrentUser, entry, date, colleagues, shifts));
    }

    private void RespondToSwap(ShiftSwapRequest req)
        => SwapDialogRequested?.Invoke(new ShiftSwapViewModel(CurrentUser, req, SwapDialogMode.Respond, SwapSummary(req)));

    private void WithdrawSwap(ShiftSwapRequest req)
        => SwapDialogRequested?.Invoke(new ShiftSwapViewModel(CurrentUser, req, SwapDialogMode.Withdraw, SwapSummary(req)));

    private string SwapSummary(ShiftSwapRequest req)
    {
        var fromLabel = ShiftLabelFor(req.FromDate, req.FromEntryId);
        if (req.Mode == SwapMode.GiveAway)
            return string.Format(Localizer.Instance["Swap_SummaryGiveAway"], req.FromUserName, fromLabel, req.ToUserName);
        var toLabel = ShiftLabelFor(req.ToDate, req.ToEntryId);
        return string.Format(Localizer.Instance["Swap_SummaryExchange"], req.FromUserName, fromLabel, req.ToUserName, toLabel);
    }

    private string ShiftLabelFor(string? dateStr, string? entryId)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        var date = DateOnly.Parse(dateStr);
        var fallback = date.ToString("ddd dd.MM.", CultureInfo.CurrentCulture);
        var e = Days.FirstOrDefault(d => d.Date == date)?.Entries.FirstOrDefault(x => x.Id == entryId);
        return e != null ? $"{fallback} {e.TimeRange}" : fallback;
    }

    /// <summary>Verarbeitet das Ergebnis des Tausch-Dialogs (Anlegen/Annehmen/Ablehnen/Zurückziehen).</summary>
    public async Task ApplySwapResultAsync(SwapDialogResult? result)
    {
        if (result == null) return;
        switch (result.Action)
        {
            case SwapDialogAction.Create:
                _swapRequests.Add(result.Request);
                await _storage.SaveSwapRequestsAsync(_swapRequests);
                LogService.UserAction(CurrentUser.Username, $"Tausch angeboten an {result.Request.ToUserName}");
                await _notifications.AddAsync(result.Request.ToUserId, "Notif_SwapOffered",
                    result.Request.FromDate, result.Request.FromUserName, FmtDate(result.Request.FromDate));
                await LoadWeekAsync();
                break;
            case SwapDialogAction.Accept:
                await AcceptSwapAsync(result.Request);
                break;
            case SwapDialogAction.Reject:
                await SetSwapStatusAsync(result.Request.Id, SwapStatus.Rejected, "Tausch abgelehnt");
                await _notifications.AddAsync(result.Request.FromUserId, "Notif_SwapRejected",
                    result.Request.FromDate, result.Request.ToUserName, FmtDate(result.Request.FromDate));
                break;
            case SwapDialogAction.Withdraw:
                await SetSwapStatusAsync(result.Request.Id, SwapStatus.Cancelled, "Tausch zurückgezogen");
                await _notifications.AddAsync(result.Request.ToUserId, "Notif_SwapWithdrawn",
                    result.Request.FromDate, result.Request.FromUserName, FmtDate(result.Request.FromDate));
                break;
        }
    }

    private async Task AcceptSwapAsync(ShiftSwapRequest req)
    {
        var fromDay = await _storage.LoadDayAsync(DateOnly.Parse(req.FromDate));
        CalendarDay? toDay = null;
        if (req.Mode == SwapMode.Exchange && !string.IsNullOrEmpty(req.ToDate))
            toDay = req.ToDate == req.FromDate ? fromDay : await _storage.LoadDayAsync(DateOnly.Parse(req.ToDate));

        var error = ShiftSwapEngine.Validate(req, fromDay, toDay);
        if (error != null) { LogService.Warn(Localizer.Instance[error]); return; }

        ShiftSwapEngine.Apply(req, fromDay, toDay);
        await _storage.SaveDayAsync(fromDay);
        if (toDay != null && !ReferenceEquals(toDay, fromDay))
            await _storage.SaveDayAsync(toDay);

        await SetSwapStatusAsync(req.Id, SwapStatus.Accepted, "Tausch angenommen");
        await _notifications.AddAsync(req.FromUserId, "Notif_SwapAccepted",
            req.FromDate, req.ToUserName, FmtDate(req.FromDate));
    }

    private static string FmtDate(string iso) => DateOnly.Parse(iso).ToString("dd.MM.yyyy");

    /// <summary>Navigiert zu der Woche, die das angegebene Datum enthält (für „zur Woche springen").</summary>
    public async Task GoToWeekContaining(DateOnly date)
    {
        WeekStart = GetMondayOfWeek(date);
        RebuildDays();
        await LoadWeekAsync();
    }

    private async Task SetSwapStatusAsync(string id, SwapStatus status, string action)
    {
        var stored = _swapRequests.FirstOrDefault(r => r.Id == id);
        if (stored != null)
        {
            stored.Status = status;
            stored.RespondedAt = DateTime.Now;
            await _storage.SaveSwapRequestsAsync(_swapRequests);
            LogService.UserAction(CurrentUser.Username, action);
        }
        await LoadWeekAsync();
    }

    private void RebuildDays()
    {
        Days.Clear();
        for (int i = 0; i < 7; i++)
            Days.Add(new CalendarDayViewModel(WeekStart.AddDays(i), this));
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

    /// <summary>Nach dem Admin-Bereich: Benutzer, Einstellungen (Region/Übernachtung), Kategorien/Regeln neu laden.</summary>
    public async Task RefreshAllAsync()
    {
        _allUsers = await _storage.LoadUsersAsync();
        RebuildUserColors();
        var settings = await _storage.LoadSettingsAsync();
        _holidayState = GermanStates.Parse(settings.HolidayState);
        _overnightHoursPerDay = settings.OvernightHoursPerDay;
        await LoadWeekAsync();  // lädt Kategorien + wiederkehrende Regeln jeweils neu
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

    /// <summary>Admin pflegt den allgemeinen Tages-Hinweis (Hinweisspalte, für alle sichtbar).</summary>
    public void RequestEditDayNote(DateOnly date)
    {
        if (!IsAdmin) return;
        var note = Days.FirstOrDefault(d => d.Date == date)?.DayNote ?? "";
        DayNoteDialogRequested?.Invoke(date, note);
    }

    public async Task ApplyDayNoteAsync(DateOnly date, string note)
    {
        var day = await _storage.LoadDayAsync(date);
        day.Note = note.Trim();
        await _storage.SaveDayAsync(day);
        var dayVm = Days.FirstOrDefault(d => d.Date == date);
        if (dayVm != null) dayVm.DayNote = day.Note;
        LogService.UserAction(CurrentUser.Username, $"Tages-Hinweis gespeichert ({date:dd.MM.yyyy})");
    }

    private void RebuildUserColors()
        => _userColors = _allUsers.ToDictionary(
            u => u.Id, u => string.IsNullOrEmpty(u.Color) ? "#7F8C8D" : u.Color);

    /// <summary>Setzt je Eintrag Personenfarbe, Deckkraft und Hervorhebung (Laufzeit, nicht persistiert).</summary>
    private void ApplyEntryDisplay(CalendarDay day)
    {
        foreach (var e in day.Entries)
        {
            e.OwnerColor = _userColors.GetValueOrDefault(e.UserId, "#7F8C8D");
            var isOwn = e.UserId == CurrentUser.Id;
            (e.EffectiveOpacity, e.IsHighlighted) = EntryDisplay.Resolve(e.Type, isOwn, IsPersonalView);

            // Datenschutz: Krank/Urlaub für Fremde als „Abwesend" ohne Grund
            var canSeeReason = IsAdmin || isOwn;
            e.DisplayType = EntryPrivacy.DisplayType(e.Type, canSeeReason);
            e.DisplayTitle = EntryPrivacy.ShowReason(e.Type, canSeeReason) ? e.Title : "";

            e.SwapMark = ResolveSwapMark(day.DateString, e.Id);

            // Aktivitäts-Kategorie auflösen (Name + Farbe), nur für sichtbare Aktivitäten
            e.ActivityName = "";
            if (e.DisplayType == EntryType.Activity && !string.IsNullOrEmpty(e.ActivityTypeId))
            {
                var type = _activityTypes.FirstOrDefault(t => t.Id == e.ActivityTypeId);
                if (type != null)
                {
                    e.ActivityName = type.Name;
                    e.ActivityColor = string.IsNullOrEmpty(type.Color) ? "#7F8C8D" : type.Color;
                }
            }
        }
    }

    /// <summary>Teilt die Tageseinträge in Raster (Arbeit/Aktivität + Nacht-Fortsetzungen + wiederkehrende) und Abwesenheits-Hinweise.</summary>
    private (List<CalendarEntry> Timeline, List<CalendarEntry> Absences) BuildDisplay(
        DateOnly date, IReadOnlyList<CalendarEntry> dayEntries, IReadOnlyList<CalendarEntry> prevDayEntries)
    {
        var timeline = new List<CalendarEntry>();
        var absences = new List<CalendarEntry>();
        foreach (var e in dayEntries)
        {
            if (EntryTypeInfo.IsAbsence(e.Type)) absences.Add(e);
            else timeline.Add(e);
        }
        timeline.AddRange(OvernightShifts.Continuations(prevDayEntries));
        timeline.AddRange(BuildRecurring(date));
        return (timeline, absences);
    }

    private bool IsHoliday(DateOnly date) => _weekHolidays.Any(h => h.Date == date);

    /// <summary>Projiziert die wiederkehrenden Regeln auf einen Tag und löst Anzeige (Farbe/Kategorie/Deckkraft) auf.</summary>
    private List<CalendarEntry> BuildRecurring(DateOnly date)
    {
        var projected = RecurrenceEngine.Project(_recurringActivities, date, IsHoliday(date));
        foreach (var e in projected) ApplyRecurringDisplay(e);
        return projected;
    }

    /// <summary>Laufzeit-Anzeige einer projizierten Aktivität (Personenfarbe, Deckkraft, Kategorie). Aktivitäten sind öffentlich.</summary>
    private void ApplyRecurringDisplay(CalendarEntry e)
    {
        e.OwnerColor = _userColors.GetValueOrDefault(e.UserId, "#7F8C8D");
        var isOwn = e.UserId == CurrentUser.Id;
        (e.EffectiveOpacity, e.IsHighlighted) = EntryDisplay.Resolve(e.Type, isOwn, IsPersonalView);
        e.DisplayType = EntryType.Activity;
        e.DisplayTitle = e.Title;

        if (!string.IsNullOrEmpty(e.ActivityTypeId))
        {
            var type = _activityTypes.FirstOrDefault(t => t.Id == e.ActivityTypeId);
            if (type != null)
            {
                e.ActivityName = type.Name;
                e.ActivityColor = string.IsNullOrEmpty(type.Color) ? "#7F8C8D" : type.Color;
            }
        }
    }

    /// <summary>Markiert eine Schicht, wenn eine offene Tausch-Anfrage sie betrifft (eingehend hat Vorrang).</summary>
    private SwapMark ResolveSwapMark(string dayStr, string entryId)
    {
        var mark = SwapMark.None;
        foreach (var r in _swapRequests)
        {
            if (r.Status != SwapStatus.Pending) continue;
            var involved = (r.FromDate == dayStr && r.FromEntryId == entryId)
                || (r.Mode == SwapMode.Exchange && r.ToDate == dayStr && r.ToEntryId == entryId);
            if (!involved) continue;
            if (CurrentUser.Id == r.ToUserId) return SwapMark.Incoming;
            if (CurrentUser.Id == r.FromUserId) mark = SwapMark.Outgoing;
        }
        return mark;
    }

    private async Task LoadWeekAsync()
    {
        LogService.Info("Lade Kalenderwoche {0}", WeekLabel);
        _swapRequests = await _storage.LoadSwapRequestsAsync();
        _activityTypes = await _storage.LoadActivityTypesAsync();
        _recurringActivities = await _storage.LoadRecurringActivitiesAsync();
        _weekHolidays = HolidayCalculator.ForRange(WeekStart, WeekStart.AddDays(6), _holidayState);

        // Vortag mitladen, damit Nacht-Schichten (z.B. So→Mo) als Fortsetzung am Folgetag erscheinen.
        var prev = await _storage.LoadDayAsync(WeekStart.AddDays(-1));
        ApplyEntryDisplay(prev);
        var prevEntries = (IReadOnlyList<CalendarEntry>)prev.Entries;

        for (int i = 0; i < 7; i++)
        {
            var date = WeekStart.AddDays(i);
            var day = await _storage.LoadDayAsync(date);
            ApplyEntryDisplay(day);
            var (timeline, absences) = BuildDisplay(date, day.Entries, prevEntries);
            Days[i].LoadFromModel(day, timeline, absences);
            Days[i].SetHoliday(_weekHolidays.FirstOrDefault(h => h.Date == date)?.NameKey, IsHolidaysVisible);
            prevEntries = day.Entries;
        }
        IsWeekFinalized = Days.Count > 0 && Days.All(d => d.IsFinalized);
        RebuildRows();
        RecomputeWeeklyHours();
    }

    /// <summary>Baut die Personen×Tag-Tabelle aus den aufgelösten Tagen (Reihenfolge: Eltern→Kinder→Angestellte→Au-Pairs).</summary>
    private void RebuildRows()
    {
        Rows.Clear();
        foreach (var u in PlanLayout.OrderPeople(_allUsers))
        {
            var isSelf = u.Id == CurrentUser.Id;
            var cells = new List<PersonDayCellViewModel>();
            foreach (var d in Days)
            {
                var entries = PlanLayout.CellEntries(d.TimelineEntries, d.AbsenceHints, u.Id);
                var canAdd = (IsAdmin && !IsPersonalView && !d.IsFinalized) || (isSelf && !IsAdmin);
                cells.Add(new PersonDayCellViewModel(d.Date, u, entries, canAdd, d.IsToday));
            }
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            var color = string.IsNullOrEmpty(u.Color) ? "#7F8C8D" : u.Color;
            Rows.Add(new PersonRowViewModel(name, color, Localizer.Instance[$"PersonCategory_{u.Category}"], isSelf, cells));
        }
    }

    /// <summary>Klick in eine Tabellenzelle: Admin plant für die Person, Mitarbeiter trägt sich krank/Urlaub ein.</summary>
    public void AddForCell(User person, DateOnly date)
    {
        if (IsAdmin && !IsPersonalView)
        {
            var finalized = Days.FirstOrDefault(d => d.Date == date)?.IsFinalized ?? false;
            if (finalized) return;
            RequestAddEntry(date, person);
        }
        else if (person.Id == CurrentUser.Id)
        {
            RequestSelfAbsence(date);
        }
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return date.AddDays(-(dow == 0 ? 6 : dow - 1));
    }
}
