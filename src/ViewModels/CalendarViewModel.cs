using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    private readonly StorageService _storage;
    private List<User> _allUsers = new();
    private List<ShiftSwapRequest> _swapRequests = new();
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

    /// <summary>date, existing (null=neu), users, canPickUser, allowedTypes. Vom CalendarView-Code-Behind abonniert.</summary>
    public event Action<DateOnly, CalendarEntry?, IReadOnlyList<User>, bool, IReadOnlyList<EntryType>>? EntryDialogRequested;

    /// <summary>Öffnet den Schichttausch-Dialog mit vorbereitetem ViewModel. Vom CalendarView-Code-Behind abonniert.</summary>
    public event Action<ShiftSwapViewModel>? SwapDialogRequested;

    private static readonly IReadOnlyList<EntryType> AllTypes = Enum.GetValues<EntryType>();

    // Selbst-Antrag: Urlaub nur wenn nicht finalisiert, Krank immer.
    private static IReadOnlyList<EntryType> AbsenceTypes(bool finalized) =>
        finalized ? new[] { EntryType.SickLeave } : new[] { EntryType.SickLeave, EntryType.Vacation };

    public CalendarViewModel(StorageService storage, User user)
    {
        _storage = storage;
        CurrentUser = user;
        // Admin startet in der Planungssicht; alle anderen fest in der Normalsicht
        _isPersonalView = user.Role != UserRole.Admin;
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
        await LoadWeekAsync();
    }

    [RelayCommand]
    private void ToggleHoursPanel()
    {
        IsHoursPanelVisible = !IsHoursPanelVisible;
        if (IsHoursPanelVisible) RecomputeWeeklyHours();
    }

    /// <summary>Ist-Stunden je Person (Work+Au-Pair) der Woche; nur Personen mit Soll&gt;0.</summary>
    private void RecomputeWeeklyHours()
    {
        var entries = Days.SelectMany(d => d.Entries).ToList();
        var actualByUser = WeeklyHoursCalculator.ActualHoursByUser(entries);
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
            .Select(d => WorkTimeRules.Summarize(d.Date, d.Entries.Where(e => e.UserId == u.Id)))
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
            foreach (var (first, second) in WorkTimeRules.WorkOverlaps(day.Entries.Where(e => e.UserId == u.Id)))
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

    public void RequestAddEntry(DateOnly date)
    {
        var users = _allUsers.Count > 0 ? _allUsers : new List<User> { CurrentUser };
        EntryDialogRequested?.Invoke(date, null, users.AsReadOnly(), true, AllTypes);
    }

    /// <summary>Selbst-Antrag: Benutzer meldet sich krank / trägt Urlaub ein (nur für sich).
    /// Krank ist immer möglich, Urlaub nur wenn die Woche nicht finalisiert ist.</summary>
    public void RequestSelfAbsence(DateOnly date)
    {
        var finalized = Days.FirstOrDefault(d => d.Date == date)?.IsFinalized ?? false;
        LogService.Click(CurrentUser.Username, $"Krank/Urlaub eintragen ({date:dd.MM.yyyy})");
        EntryDialogRequested?.Invoke(date, null, new List<User> { CurrentUser }.AsReadOnly(),
            false, AbsenceTypes(finalized));
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
        EntryDialogRequested?.Invoke(date, entry, users.AsReadOnly(), adminEdit, allowed);
    }

    /// <summary>Speichert/löscht das Dialog-Ergebnis: ein Pfad für Neu, Edit und Delete.</summary>
    public async Task ApplyEntryResultAsync(DateOnly date, EntryDialogResult result)
    {
        var day = await _storage.LoadDayAsync(date);
        day.Entries.RemoveAll(e => e.Id == result.Entry.Id);
        if (result.Action == EntryDialogAction.Save)
            day.Entries.Add(result.Entry);
        day.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        await _storage.SaveDayAsync(day);

        ApplyEntryDisplay(day);
        var dayVm = Days.FirstOrDefault(d => d.Date == date);
        dayVm?.LoadFromModel(day);
        RecomputeWeeklyHours();

        var verb = result.Action == EntryDialogAction.Save ? "gespeichert" : "gelöscht";
        LogService.UserAction(CurrentUser.Username,
            $"Eintrag {verb}: {result.Entry.TypeLabel} für {result.Entry.UserDisplayName} am {date:dd.MM.yyyy}");
    }

    /// <summary>Antippen einer Schicht: offene Anfrage beantworten/zurückziehen, eigenen Tausch anbieten oder bearbeiten.</summary>
    public void ActivateEntry(DateOnly date, CalendarEntry entry)
    {
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

        var finalized = Days.FirstOrDefault(d => d.Date == date)?.IsFinalized ?? false;
        if (!IsAdmin && entry.UserId == CurrentUser.Id && entry.Type == EntryType.Work && !finalized)
        {
            RequestInitiateSwap(date, entry);
            return;
        }

        RequestEditEntry(date, entry);
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
                await LoadWeekAsync();
                break;
            case SwapDialogAction.Accept:
                await AcceptSwapAsync(result.Request);
                break;
            case SwapDialogAction.Reject:
                await SetSwapStatusAsync(result.Request.Id, SwapStatus.Rejected, "Tausch abgelehnt");
                break;
            case SwapDialogAction.Withdraw:
                await SetSwapStatusAsync(result.Request.Id, SwapStatus.Cancelled, "Tausch zurückgezogen");
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
        await LoadWeekAsync();
    }

    /// <summary>Personenliste neu laden (frische Benutzer sofort planbar) inkl. Farben → Woche neu laden.</summary>
    public async Task ReloadUsersAsync()
    {
        _allUsers = await _storage.LoadUsersAsync();
        RebuildUserColors();
        await LoadWeekAsync();  // damit geänderte Personenfarben sofort sichtbar werden
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
        for (int i = 0; i < 7; i++)
        {
            var day = await _storage.LoadDayAsync(WeekStart.AddDays(i));
            ApplyEntryDisplay(day);
            Days[i].LoadFromModel(day);
        }
        IsWeekFinalized = Days.Count > 0 && Days.All(d => d.IsFinalized);
        RecomputeWeeklyHours();
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return date.AddDays(-(dow == 0 ? 6 : dow - 1));
    }
}
