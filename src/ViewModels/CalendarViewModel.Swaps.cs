using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Schichttausch und Umplanung: Anfragen stellen, beantworten, zurückziehen — plus die
/// Ersatzsuche, wenn jemand ausfällt.
/// </summary>
public partial class CalendarViewModel
{
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
        var req = result.Request;
        switch (result.Action)
        {
            case SwapDialogAction.Create:
                // Der gespeicherte Vorschlag zählt, nicht der aus dem Dialog: im Server-Modus setzt
                // der Server Id, Namen und Datum aus der Datenbank.
                var created = await _storage.CreateSwapRequestAsync(req);
                LogService.UserAction(CurrentUser.Username, $"Tausch angeboten an {created.ToUserName}");
                await _notifications.AddAsync(created.ToUserId, "Notif_SwapOffered",
                    created.FromDate, created.FromUserName, FmtDate(created.FromDate));
                break;
            case SwapDialogAction.Accept:
                var error = await _storage.AcceptSwapRequestAsync(req);
                if (error != null)
                {
                    LogService.Warn(Localizer.Instance[error]);
                    break;
                }
                LogService.UserAction(CurrentUser.Username, "Tausch angenommen");
                await _notifications.AddAsync(req.FromUserId, "Notif_SwapAccepted",
                    req.FromDate, req.ToUserName, FmtDate(req.FromDate));
                break;
            case SwapDialogAction.Reject:
                await _storage.RejectSwapRequestAsync(req.Id);
                LogService.UserAction(CurrentUser.Username, "Tausch abgelehnt");
                await _notifications.AddAsync(req.FromUserId, "Notif_SwapRejected",
                    req.FromDate, req.ToUserName, FmtDate(req.FromDate));
                break;
            case SwapDialogAction.Withdraw:
                await _storage.WithdrawSwapRequestAsync(req.Id);
                LogService.UserAction(CurrentUser.Username, "Tausch zurückgezogen");
                await _notifications.AddAsync(req.ToUserId, "Notif_SwapWithdrawn",
                    req.FromDate, req.FromUserName, FmtDate(req.FromDate));
                break;
        }
        await LoadWeekAsync();
    }

    private static string FmtDate(string iso) => DateOnly.Parse(iso).ToString("dd.MM.yyyy");
}
