using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// KI-Planer: Vorschläge des Assistenten auf die Woche anwenden.
/// Additiv gedacht — ohne KI funktioniert der Kalender vollständig, und jeder Vorschlag
/// geht vor dem Anwenden durch <see cref="ValidateAiSuggestion"/>.
/// </summary>
public partial class CalendarViewModel
{
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
}
