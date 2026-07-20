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
    [NotifyPropertyChangedFor(nameof(IsImpersonating), nameof(ViewAsBanner),
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
        // Personalisierte Hinweise: jeder Empfänger sieht nur die für ihn relevanten (oder alle, wenn Admin).
        var notes = Days.Select(d =>
            PlanExportBuilder.NoteFor(d.RawNote, d.NoteUserId, isAdmin, viewer.Id)).ToList();

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

    /// <summary>Öffnet den KI-Planungs-Chat (Admin). Kontext = aktuelle Personen, Regeln und Wochen-Einträge.</summary>
    [RelayCommand]
    private async Task OpenAiPlanner()
    {
        if (!IsAdmin || App.DialogService is null) return;
        var chat = new Services.AI.AiChatService(_ai);
        var vm = new AiPlannerViewModel(_storage, _ai, chat, BuildPlannerContext,
            ApplyAiSuggestionAsync, ValidateAiSuggestion, GoToWeekContaining);
        LogService.Click(CurrentUser.Username, "KI-Planner geöffnet");
        await App.DialogService.ShowAiPlannerAsync(vm);
    }

    /// <summary>
    /// Übernimmt einen KI-Vorschlag als echte Kalender-Mutation. Drei Aktionen: Add legt einen
    /// neuen Eintrag an, Update ändert Zeit/Titel an einem bestehenden, Delete entfernt ihn.
    /// Anschließend silent Refresh, damit die Karte direkt sichtbar wird.
    /// </summary>
    private async Task<bool> ApplyAiSuggestionAsync(Services.AI.PlannerSuggestion s)
    {
        // Pause/Resume/Swap sind Sonderfälle — schreiben nicht in den Tages-Storage.
        if (s.Action == Services.AI.SuggestionAction.Pause)
            return await ApplyPauseAsync(s);
        if (s.Action == Services.AI.SuggestionAction.Resume)
            return await ApplyResumeAsync(s);
        if (s.Action == Services.AI.SuggestionAction.Swap)
            return await ApplySwapAsync(s);

        var day = await _storage.LoadDayAsync(s.Date);
        bool changed;

        switch (s.Action)
        {
            case Services.AI.SuggestionAction.Add:
                changed = ApplyAdd(day, s);
                break;
            case Services.AI.SuggestionAction.Update:
                changed = ApplyUpdate(day, s);
                break;
            case Services.AI.SuggestionAction.Delete:
                changed = day.Entries.RemoveAll(e => e.Id == s.EntryId) > 0;
                if (changed) LogService.UserAction("Admin", $"KI-Vorschlag übernommen: Löschen {s.EntryId} am {s.Date}");
                break;
            default:
                return false;
        }

        if (!changed) return false;
        day.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        await _storage.SaveDayAsync(day);
        await RefreshAllAsync(silent: true);
        return true;
    }

    private async Task<bool> ApplyPauseAsync(Services.AI.PlannerSuggestion s)
    {
        if (s.RecurringActivityId is null || s.From is null || s.To is null) return false;
        var rule = _recurringActivities.FirstOrDefault(r => r.Id == s.RecurringActivityId);
        if (rule is null) { LogService.Warn("KI-Vorschlag: Regel {0} nicht gefunden", s.RecurringActivityId); return false; }

        rule.Skips.Add(new RecurrenceSkip
        {
            From = s.From.Value,
            To = s.To.Value,
            Reason = string.IsNullOrWhiteSpace(s.Reason) ? null : s.Reason!.Trim()
        });
        await _storage.SaveRecurringActivitiesAsync(_recurringActivities);
        LogService.UserAction("Admin",
            $"KI-Vorschlag übernommen: Pause für {rule.Title} {s.From:dd.MM.}–{s.To:dd.MM.}");
        await RefreshAllAsync(silent: true);
        return true;
    }

    /// <summary>
    /// Übernimmt einen Schicht-Tausch-Vorschlag der KI: legt einen ShiftSwapRequest in der
    /// üblichen Pending-Form an, sodass der Empfänger ihn im Kalender bestätigt/ablehnt.
    /// Initiator ist der Owner der FromEntry-Schicht — der Admin ist hier Vermittler.
    /// </summary>
    private async Task<bool> ApplySwapAsync(Services.AI.PlannerSuggestion s)
    {
        if (string.IsNullOrEmpty(s.FromEntryId) || string.IsNullOrEmpty(s.ToUserId)) return false;

        // From-Eintrag im aktuellen Tag-Snapshot suchen — er muss real existieren.
        CalendarEntry? fromEntry = null;
        DateOnly fromDate = default;
        foreach (var d in Days)
        {
            var e = d.Entries.FirstOrDefault(x => x.Id == s.FromEntryId && !x.IsRecurring);
            if (e is not null) { fromEntry = e; fromDate = d.Date; break; }
        }
        if (fromEntry is null) { LogService.Warn("KI-Vorschlag Swap: From-Schicht {0} nicht gefunden", s.FromEntryId); return false; }

        var toUser = _allUsers.FirstOrDefault(u => u.Id == s.ToUserId);
        if (toUser is null) { LogService.Warn("KI-Vorschlag Swap: Empfänger {0} nicht gefunden", s.ToUserId); return false; }

        var mode = string.Equals(s.SwapMode, "exchange", StringComparison.OrdinalIgnoreCase)
            ? SwapMode.Exchange : SwapMode.GiveAway;

        CalendarEntry? toEntry = null;
        DateOnly toDate = default;
        if (mode == SwapMode.Exchange)
        {
            if (string.IsNullOrEmpty(s.ToEntryId)) return false;
            foreach (var d in Days)
            {
                var e = d.Entries.FirstOrDefault(x => x.Id == s.ToEntryId && !x.IsRecurring);
                if (e is not null) { toEntry = e; toDate = d.Date; break; }
            }
            if (toEntry is null) { LogService.Warn("KI-Vorschlag Swap: Gegen-Schicht {0} nicht gefunden", s.ToEntryId); return false; }
        }

        var req = new ShiftSwapRequest
        {
            Mode = mode,
            FromUserId = fromEntry.UserId,
            FromUserName = fromEntry.UserDisplayName,
            FromDate = fromDate.ToString("yyyy-MM-dd"),
            FromEntryId = fromEntry.Id,
            ToUserId = toUser.Id,
            ToUserName = string.IsNullOrEmpty(toUser.DisplayName) ? toUser.Username : toUser.DisplayName,
            ToDate = toEntry is null ? null : toDate.ToString("yyyy-MM-dd"),
            ToEntryId = toEntry?.Id,
            Message = s.Message ?? ""
        };
        _swapRequests.Add(req);
        await _storage.SaveSwapRequestsAsync(_swapRequests);
        await _notifications.AddAsync(req.ToUserId, "Notif_SwapOffered",
            req.FromDate, req.FromUserName, FmtDate(req.FromDate));
        LogService.UserAction("Admin",
            $"KI-Vorschlag übernommen: Tausch {req.FromUserName} → {req.ToUserName} ({mode}) {fromDate:dd.MM.}");
        await RefreshAllAsync(silent: true);
        return true;
    }

    private async Task<bool> ApplyResumeAsync(Services.AI.PlannerSuggestion s)
    {
        if (s.RecurringActivityId is null || s.SkipId is null) return false;
        var rule = _recurringActivities.FirstOrDefault(r => r.Id == s.RecurringActivityId);
        if (rule is null) { LogService.Warn("KI-Vorschlag: Regel {0} nicht gefunden", s.RecurringActivityId); return false; }
        var skip = rule.Skips.FirstOrDefault(x => x.Id == s.SkipId);
        if (skip is null) { LogService.Warn("KI-Vorschlag: Pause {0} an Regel {1} nicht gefunden", s.SkipId, rule.Id); return false; }

        rule.Skips.Remove(skip);
        await _storage.SaveRecurringActivitiesAsync(_recurringActivities);
        LogService.UserAction("Admin",
            $"KI-Vorschlag übernommen: Pause {skip.From:dd.MM.}–{skip.To:dd.MM.} für {rule.Title} aufgehoben");
        await RefreshAllAsync(silent: true);
        return true;
    }

    private bool ApplyAdd(CalendarDay day, Services.AI.PlannerSuggestion s)
    {
        if (s.UserId is null || s.Type is null || s.Start is null || s.End is null) return false;
        var user = _allUsers.FirstOrDefault(u => u.Id == s.UserId);
        if (user is null) { LogService.Warn("KI-Vorschlag: unbekannte UserId {0}", s.UserId); return false; }
        var entry = new CalendarEntry
        {
            UserId = user.Id,
            UserDisplayName = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName,
            Type = s.Type.Value,
            StartTime = s.Start.Value,
            EndTime = s.End.Value,
            Title = s.Title ?? ""
        };
        day.Entries.Add(entry);
        LogService.UserAction("Admin", $"KI-Vorschlag übernommen: {entry.UserDisplayName} {s.Date} {entry.TimeRange} {entry.Type}");
        return true;
    }

    private bool ApplyUpdate(CalendarDay day, Services.AI.PlannerSuggestion s)
    {
        var entry = day.Entries.FirstOrDefault(e => e.Id == s.EntryId);
        if (entry is null) { LogService.Warn("KI-Vorschlag: Eintrag {0} nicht gefunden", s.EntryId); return false; }
        if (s.Start is { } st) entry.StartTime = st;
        if (s.End is { } en) entry.EndTime = en;
        if (s.Title is not null) entry.Title = s.Title;
        if (s.Type is { } et) entry.Type = et;
        if (s.UserId is { Length: > 0 } uid && uid != entry.UserId)
        {
            var newUser = _allUsers.FirstOrDefault(u => u.Id == uid);
            if (newUser is null) { LogService.Warn("KI-Vorschlag: Update auf unbekannte UserId {0}", uid); return false; }
            entry.UserId = newUser.Id;
            entry.UserDisplayName = string.IsNullOrEmpty(newUser.DisplayName) ? newUser.Username : newUser.DisplayName;
        }
        LogService.UserAction("Admin", $"KI-Vorschlag übernommen: Update {entry.Id} → {entry.UserDisplayName} {entry.TimeRange} {entry.Type} {entry.Title}");
        return true;
    }

    /// <summary>Prüft einen KI-Vorschlag gegen die aktuelle Wochenlage. Reine Reichweite zum Validator-Helper.</summary>
    private IReadOnlyList<Services.AI.SuggestionWarning> ValidateAiSuggestion(Services.AI.PlannerSuggestion s)
    {
        var ctx = BuildPlannerContext();
        return Services.AI.PlannerSuggestionValidator.Validate(s, ctx.Users, ctx.Week);
    }

    /// <summary>Schnappschuss der aktuell sichtbaren Woche für den KI-Kontext-Block. Notes werden im VM nachgeladen.</summary>
    private Services.AI.PlannerContext BuildPlannerContext()
    {
        var weekTuples = Days
            .Select(d => (d.Date, (IReadOnlyList<CalendarEntry>)d.Entries.ToList()))
            .ToList();
        return new Services.AI.PlannerContext(
            Today: DateOnly.FromDateTime(DateTime.Today),
            WeekStart: WeekStart,
            Users: _allUsers,
            ActivityTypes: _activityTypes,
            RecurringActivities: _recurringActivities,
            Week: weekTuples,
            Notes: Array.Empty<Models.PlannerNote>(),
            ViewerName: string.IsNullOrEmpty(CurrentUser.DisplayName) ? CurrentUser.Username : CurrentUser.DisplayName,
            ViewerStyleHint: CurrentUser.AiStyleHint);
    }

    /// <summary>Plan per E-Mail senden: prüft die SMTP-Konfiguration und öffnet die Empfänger-Auswahl (nur Admin).</summary>
    [RelayCommand]
    private async Task MailPlan()
    {
        if (!IsAdmin) return;
        // Local-Modus prüft SMTP-Settings hier; Server-Modus lässt durch (Server entscheidet beim Senden).
        if (!await _mailSender.IsConfiguredAsync()) { LogService.Warn(Localizer.Instance["Mail_NotConfigured"]); return; }
        var recipients = MailComposer.RecipientsWithEmail(_allUsers);
        if (recipients.Count == 0) { LogService.Warn(Localizer.Instance["Mail_NoRecipients"]); return; }

        LogService.Click(CurrentUser.Username, $"Mail-Versand ({WeekLabel})");
        MailDialogRequested?.Invoke(new MailViewModel(recipients));
    }

    /// <summary>Sendet jedem Empfänger ein eigenes, aus seiner Sicht maskiertes Wochen-PDF (Local: pro Adresse
    /// ein SmtpClient.SendMail; Server: ein Batch-POST an /api/mail/send-week-plan).</summary>
    public async Task SendPlanMailAsync(IReadOnlyList<string> emails)
    {
        if (emails.Count == 0) return;
        var subject = $"{Localizer.Instance["Pdf_Title"]} {WeekLabel}";
        var body = string.Format(Localizer.Instance["Mail_Body"], WeekLabel);

        // PDFs pro Empfänger client-seitig rendern (Datenschutz: jeder bekommt seine Sicht).
        var items = new List<MailSendItem>();
        foreach (var email in emails)
        {
            var viewer = _allUsers.FirstOrDefault(u =>
                u.Email.Trim().Equals(email, StringComparison.OrdinalIgnoreCase));
            if (viewer is null) continue;
            var pdf = PdfExportService.Render(CreateWeekExport(viewer));
            items.Add(new MailSendItem(email, pdf));
        }
        if (items.Count == 0) { LogService.Warn(Localizer.Instance["Mail_NoRecipients"]); return; }

        try
        {
            var result = await _mailSender.SendWeekPlanAsync(subject, body, ExportFileName, items);
            LogService.Info(string.Format(Localizer.Instance["Mail_Sent"], result.Sent));
            foreach (var err in result.Errors)
                LogService.Warn("Mail: {0}", err);
        }
        catch (Exception ex)
        {
            LogService.Error("Mail-Versand fehlgeschlagen", ex);
        }
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
        // Projektionen sind keine echten Tageseinträge — Admin kann sie aber pausieren (Urlaub/Krank).
        if (entry.IsRecurring)
        {
            if (IsAdmin) _ = ManageRecurringPauseAsync(date, entry);
            return;
        }

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

    /// <summary>
    /// Admin pausiert/reaktiviert eine wiederkehrende Aktivität tagesgenau. Die Pausen-Liste
    /// wird im Dialog bearbeitet und anschließend mit der ganzen Regel-Liste persistiert.
    /// </summary>
    private async Task ManageRecurringPauseAsync(DateOnly date, CalendarEntry projected)
    {
        LogService.Debug("Pause-Dialog angefragt: date={0}, entryId={1}, DialogService={2}",
            date, projected.Id, App.DialogService is null ? "null" : App.DialogService.GetType().Name);
        if (App.DialogService is null) return;

        // Id-Format: "recurring:{ruleId}:{yyyy-MM-dd}" — Mittelteil ist die Rule-Id.
        var parts = projected.Id.Split(':');
        if (parts.Length < 3) { LogService.Warn("Recurring-Id-Format unerwartet: {0}", projected.Id); return; }
        var ruleId = parts[1];
        var rule = _recurringActivities.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) { LogService.Warn("Regel {0} nicht gefunden", ruleId); return; }

        var vm = new RecurrencePauseViewModel(rule, date);
        var result = await App.DialogService.ShowRecurrencePauseAsync(vm);
        if (result is null) { LogService.Debug("Pause-Dialog abgebrochen"); return; }

        rule.Skips = result.ToList();
        await _storage.SaveRecurringActivitiesAsync(_recurringActivities);
        LogService.UserAction("Admin", $"Aussetzungen für {rule.Title} aktualisiert ({result.Count})");
        await RefreshAllAsync(silent: true);
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

    /// <summary>Tages-Hinweis pflegen (Admin oder Eltern). Sichtbarkeit pro Eintrag: null = alle, sonst Admin + Adressat.</summary>
    public void RequestEditDayNote(DateOnly date)
    {
        if (!CanFinalize) return;
        var dayVm = Days.FirstOrDefault(d => d.Date == date);
        var note = dayVm?.RawNote ?? "";
        var assigned = dayVm?.NoteUserId;
        DayNoteDialogRequested?.Invoke(date, note, assigned);
    }

    public async Task ApplyDayNoteAsync(DateOnly date, string note, string? noteUserId)
    {
        var day = await _storage.LoadDayAsync(date);
        day.Note = note.Trim();
        day.NoteUserId = string.IsNullOrWhiteSpace(noteUserId) ? null : noteUserId;
        await _storage.SaveDayAsync(day);
        var dayVm = Days.FirstOrDefault(d => d.Date == date);
        if (dayVm != null)
        {
            dayVm.SetNote(day.Note, day.NoteUserId, CanSeeNote(day.NoteUserId));
        }
        LogService.UserAction(CurrentUser.Username, $"Tages-Hinweis gespeichert ({date:dd.MM.yyyy})");
    }

    /// <summary>Sichtbarkeitsregel: null = alle; sonst nur Admin und die adressierte Person (auch unter View-as).</summary>
    private bool CanSeeNote(string? noteUserId)
    {
        if (string.IsNullOrEmpty(noteUserId)) return true;
        if (EffectiveIsAdmin) return true;
        return noteUserId == EffectiveUserId;
    }

    private void RebuildUserColors()
        => _userColors = _allUsers.ToDictionary(
            u => u.Id, u => string.IsNullOrEmpty(u.Color) ? "#7F8C8D" : u.Color);

    /// <summary>
    /// Spiegelt die serverseitige EntryVisibility-Regel clientseitig für den View-as-Modus.
    /// Der Admin bekommt vom Server (aus Effizienzgründen) alle Einträge — beim Impersonate
    /// soll er aber nur das sehen, was der beobachtete Nicht-Admin-Kollege sehen würde:
    /// nichts von Fremden vor Finalisierung, eigene Work erst nach Finalisierung.
    /// Ohne Impersonate wird die Liste unverändert zurückgegeben.
    /// </summary>
    private IReadOnlyList<CalendarEntry> EntriesVisibleUnderImpersonation(CalendarDay day)
    {
        if (!IsImpersonating) return day.Entries;
        var effUserId = EffectiveUserId;
        var result = new List<CalendarEntry>(day.Entries.Count);
        foreach (var e in day.Entries)
        {
            var isOwner = e.UserId == effUserId;
            if (!isOwner)
            {
                if (!day.IsFinalized) continue;
                // Server würde hier Pending/Rejected wegwerfen — im Server-Modus kommen die
                // ohnehin nur maskiert an; wir filtern hier nur die Finalisierung nach.
            }
            else if (e.Type == EntryType.Work && !day.IsFinalized)
            {
                continue;
            }
            result.Add(e);
        }
        return result;
    }

    /// <summary>Setzt je Eintrag Personenfarbe, Deckkraft und Hervorhebung (Laufzeit, nicht persistiert).</summary>
    private void ApplyEntryDisplay(CalendarDay day)
    {
        foreach (var e in day.Entries)
        {
            e.OwnerColor = _userColors.GetValueOrDefault(e.UserId, "#7F8C8D");
            // ServerEntryDto liefert keinen DisplayName mit — im Server-Modus ist e.UserDisplayName
            // deshalb leer. Aus den geladenen Benutzern nachschlagen, damit UI-Bindings (v.a. der
            // Mobile-Kalender, der pro Zeile einen Namen zeigt) einen Anzeigenamen bekommen.
            if (string.IsNullOrEmpty(e.UserDisplayName))
            {
                var owner = _allUsers.FirstOrDefault(u => u.Id == e.UserId);
                if (owner is not null)
                    e.UserDisplayName = string.IsNullOrEmpty(owner.DisplayName) ? owner.Username : owner.DisplayName;
            }
            var isOwn = e.UserId == EffectiveUserId;

            // Datenschutz: Krank/Urlaub für Fremde als „Abwesend" ohne Grund
            var canSeeReason = EffectiveIsAdmin || isOwn;
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

    /// <summary>Teilt die Tageseinträge in Raster (Arbeit/Aktivität + wiederkehrende) und Abwesenheits-Hinweise.</summary>
    private (List<CalendarEntry> Timeline, List<CalendarEntry> Absences) BuildDisplay(
        DateOnly date, IReadOnlyList<CalendarEntry> dayEntries)
    {
        var timeline = new List<CalendarEntry>();
        var absences = new List<CalendarEntry>();
        foreach (var e in dayEntries)
        {
            if (EntryTypeInfo.IsAbsence(e.Type)) absences.Add(e);
            else timeline.Add(e);
        }
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

    private async Task LoadWeekAsync(bool silent = false)
    {
        if (!silent) LogService.Info("Lade Kalenderwoche {0}", WeekLabel);
        _swapRequests = await _storage.LoadSwapRequestsAsync();
        _activityTypes = await _storage.LoadActivityTypesAsync();
        _recurringActivities = await _storage.LoadRecurringActivitiesAsync();
        _weekHolidays = HolidayCalculator.ForRange(WeekStart, WeekStart.AddDays(6), _holidayState);

        // (Vortag wurde früher für die Nacht-Fortsetzungs-Anzeige geladen — die Tabellen-Sicht
        // braucht das nicht mehr; die Nacht-Schicht steht jetzt nur am Starttag.)

        for (int i = 0; i < 7; i++)
        {
            var date = WeekStart.AddDays(i);
            var day = await _storage.LoadDayAsync(date);
            ApplyEntryDisplay(day);
            var entries = EntriesVisibleUnderImpersonation(day);
            var (timeline, absences) = BuildDisplay(date, entries);
            Days[i].LoadFromModel(day, timeline, absences, CanSeeNote(day.NoteUserId));
            Days[i].SetHoliday(_weekHolidays.FirstOrDefault(h => h.Date == date)?.NameKey, IsHolidaysVisible);
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
            var isSelf = u.Id == EffectiveUserId;
            var cells = new List<PersonDayCellViewModel>();
            foreach (var d in Days)
            {
                var entries = PlanLayout.CellEntries(d.TimelineEntries, d.AbsenceHints, u.Id);
                var canAdd = (EffectiveIsAdmin && !IsPersonalView && !d.IsFinalized) || (isSelf && !EffectiveIsAdmin);
                cells.Add(new PersonDayCellViewModel(d.Date, u, entries, canAdd, d.IsToday));
            }
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            var color = string.IsNullOrEmpty(u.Color) ? "#7F8C8D" : u.Color;
            // Admin-only Klick auf den Personennamen → View-as auf diese Person.
            var impersonateCmd = IsAdmin ? ToggleImpersonationCommand : null;
            var rowCmd = impersonateCmd is null
                ? (IRelayCommand?)null
                : new CommunityToolkit.Mvvm.Input.RelayCommand(() => impersonateCmd.Execute(u.Id));
            // Admin darf die Personen-Reihenfolge in der Planansicht per Drag&Drop pflegen —
            // View-as-Modus zeigt eine Nicht-Admin-Sicht, dort kein Reorder.
            var canReorder = EffectiveIsAdmin && !IsPersonalView;
            Rows.Add(new PersonRowViewModel(u.Id, name, color, Localizer.Instance[$"PersonCategory_{u.Category}"], isSelf, cells, rowCmd, canReorder));
        }
    }

    /// <summary>
    /// Admin-Aktion: Personenzeile <paramref name="sourceUserId"/> per Drag&amp;Drop in der Plansicht
    /// an die Stelle von <paramref name="targetUserId"/> setzen. Berechnet die neue vollständige
    /// Reihenfolge, persistiert sie über den Storage und baut die Zeilen neu.
    /// </summary>
    public async Task ReorderPersonAsync(string sourceUserId, string targetUserId)
    {
        if (!EffectiveIsAdmin) return;
        if (string.IsNullOrEmpty(sourceUserId) || string.IsNullOrEmpty(targetUserId)) return;
        if (sourceUserId == targetUserId) return;

        // In der aktuellen Reihenfolge arbeiten, damit die UI ohne erneutes Serverladen konsistent bleibt.
        var ordered = PlanLayout.OrderPeople(_allUsers).ToList();
        var src = ordered.FirstOrDefault(u => u.Id == sourceUserId);
        var tgt = ordered.FirstOrDefault(u => u.Id == targetUserId);
        if (src is null || tgt is null) return;

        ordered.Remove(src);
        var targetIndex = ordered.IndexOf(tgt);
        if (targetIndex < 0) return;
        ordered.Insert(targetIndex, src);

        var ids = ordered.Select(u => u.Id).ToList();

        try
        {
            await _storage.ReorderUsersAsync(ids);
        }
        catch (Exception ex)
        {
            LogService.Error("Personen-Reihenfolge konnte nicht gespeichert werden", ex);
            return;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            var u = _allUsers.FirstOrDefault(x => x.Id == ids[i]);
            if (u is not null) u.PlanOrder = i;
        }
        RebuildRows();
        LogService.UserAction(CurrentUser.Username, $"Personen-Reihenfolge geändert ({ids.Count} Personen)");
    }

    /// <summary>
    /// Drag&amp;Drop einer Schicht von einer Zelle auf eine andere (Person oder Tag). Öffnet den
    /// „Verschieben/Kopieren?"-Dialog und führt das Resultat aus. Nur Admin (Nicht-Admins nutzen
    /// den Schichttausch-Workflow). Abwesenheiten, wiederkehrende Overlays und finalisierte Tage
    /// werden bewusst ignoriert.
    /// </summary>
    public async Task HandleEntryDropAsync(string entryId, DateOnly sourceDate, PersonDayCellViewModel target)
    {
        if (!IsAdmin) return;
        if (App.DialogService is null) return;

        var source = Days.SelectMany(d => d.Entries).FirstOrDefault(e => e.Id == entryId);
        if (source is null) return;

        // Engine entscheidet Erlaubnis (Recurring, Abwesenheit, No-Op).
        var probe = EntryMoveCopy.Plan(source, sourceDate, target.Date, target.Person.Id,
            target.Person.DisplayName ?? target.Person.Username, MoveCopyAction.Move);
        if (probe is null) return;

        // Drop in finalisierte Wochen vorerst sperren — sonst überschriebene Genehmigungen.
        var targetDay = Days.FirstOrDefault(d => d.Date == target.Date);
        if (targetDay?.IsFinalized == true) { LogService.Warn("Drop in finalisierter Woche abgelehnt."); return; }

        var personLabel = string.IsNullOrEmpty(target.Person.DisplayName) ? target.Person.Username : target.Person.DisplayName;
        var description = string.Format(
            Localizer.Instance["MoveCopy_Description"],
            $"{source.UserDisplayName} {sourceDate:dd.MM.}",
            $"{personLabel} {target.Date:dd.MM.}");

        var dialogVm = new MoveCopyViewModel(Localizer.Instance["MoveCopy_Title"], description);
        var result = await App.DialogService.ShowMoveCopyAsync(dialogVm);
        if (result is null) return;

        var plan = EntryMoveCopy.Plan(source, sourceDate, target.Date, target.Person.Id,
            personLabel, result.Action);
        if (plan is null) return;

        if (plan.Delete is not null && plan.DeleteFromDate is not null)
        {
            await ApplyEntryResultAsync(plan.DeleteFromDate.Value,
                new EntryDialogResult(EntryDialogAction.Delete, plan.Delete,
                    plan.DeleteFromDate.Value, plan.DeleteFromDate.Value));
        }
        await ApplyEntryResultAsync(plan.SaveToDate,
            new EntryDialogResult(EntryDialogAction.Save, plan.Save, plan.SaveToDate, plan.SaveToDate));

        LogService.UserAction(CurrentUser.Username,
            $"Eintrag {(result.Action == MoveCopyAction.Move ? "verschoben" : "kopiert")}: " +
            $"{source.UserDisplayName} {sourceDate:dd.MM.} → {personLabel} {target.Date:dd.MM.}");
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
