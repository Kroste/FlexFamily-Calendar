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
            .Where(e => e.UserId == absentUserId && e.Type == EntryType.Work && e.IsShift)
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
                // Nur eintägige Schichten — ein mehrtägiger Einsatz lässt sich nicht tageweise tauschen.
                if (e.Type == EntryType.Work && !e.IsMultiDay && e.Id != entry.Id && colleagueIds.Contains(e.UserId))
                    shifts.Add(new SwapShiftOption(e.Id, d.Date.ToString("yyyy-MM-dd"), e.UserId,
                        $"{d.Date.ToString("ddd dd.MM.", CultureInfo.CurrentCulture)} {e.TimeRange}"));

        LogService.Click(CurrentUser.Username, $"Tausch anbieten ({date:dd.MM.yyyy})");
        SwapDialogRequested?.Invoke(new ShiftSwapViewModel(CurrentUser, entry, date, colleagues, shifts, ExecuteSwapAsync));
    }

    private void RespondToSwap(ShiftSwapRequest req)
        => SwapDialogRequested?.Invoke(new ShiftSwapViewModel(CurrentUser, req, SwapDialogMode.Respond, SwapSummary(req), ExecuteSwapAsync));

    private void WithdrawSwap(ShiftSwapRequest req)
        => SwapDialogRequested?.Invoke(new ShiftSwapViewModel(CurrentUser, req, SwapDialogMode.Withdraw, SwapSummary(req), ExecuteSwapAsync));

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

    /// <summary>
    /// Führt eine Tausch-Aktion aus, während der Dialog noch offen ist. Gibt <c>null</c> zurück,
    /// wenn sie durchging, sonst eine anzeigefertige Meldung — der Dialog zeigt sie und bleibt offen.
    /// </summary>
    public async Task<string?> ExecuteSwapAsync(SwapDialogResult result)
    {
        var req = result.Request;
        try
        {
            switch (result.Action)
            {
                case SwapDialogAction.Create:
                    // Der gespeicherte Vorschlag zählt, nicht der aus dem Dialog: im Server-Modus
                    // setzt der Server Id, Namen und Datum aus der Datenbank.
                    req = await _storage.CreateSwapRequestAsync(req);
                    LogService.UserAction(CurrentUser.Username, $"Tausch angeboten an {req.ToUserName}");
                    await NotifyQuietlyAsync(req.ToUserId, "Notif_SwapOffered", req, req.FromUserName);
                    break;
                case SwapDialogAction.Accept:
                    if (await _storage.AcceptSwapRequestAsync(req) is { } key)
                    {
                        LogService.Warn("Tausch nicht angenommen: {0}", Localizer.Instance[key]);
                        return Localizer.Instance[key];
                    }
                    LogService.UserAction(CurrentUser.Username, "Tausch angenommen");
                    await NotifyQuietlyAsync(req.FromUserId, "Notif_SwapAccepted", req, req.ToUserName);
                    break;
                case SwapDialogAction.Reject:
                    await _storage.RejectSwapRequestAsync(req.Id);
                    LogService.UserAction(CurrentUser.Username, "Tausch abgelehnt");
                    await NotifyQuietlyAsync(req.FromUserId, "Notif_SwapRejected", req, req.ToUserName);
                    break;
                case SwapDialogAction.Withdraw:
                    await _storage.WithdrawSwapRequestAsync(req.Id);
                    LogService.UserAction(CurrentUser.Username, "Tausch zurückgezogen");
                    await NotifyQuietlyAsync(req.ToUserId, "Notif_SwapWithdrawn", req, req.FromUserName);
                    break;
            }
            return null;
        }
        catch (Exception ex)
        {
            // Netz weg, Server lehnt ab, Vorschlag inzwischen erledigt — alles zeigt der Dialog an.
            LogService.Error($"Tausch-Aktion {result.Action} fehlgeschlagen", ex);
            return ex.Message;
        }
    }

    /// <summary>
    /// Die Benachrichtigung ist Beiwerk: ist der Tausch schon gebucht, darf ein Fehler beim Senden
    /// ihn nicht als gescheitert melden — der Nutzer würde es erneut versuchen und bekäme dann
    /// „bereits erledigt".
    /// </summary>
    private async Task NotifyQuietlyAsync(string userId, string key, ShiftSwapRequest req, string who)
    {
        try
        {
            await _notifications.AddAsync(userId, key, req.FromDate, who, FmtDate(req.FromDate));
        }
        catch (Exception ex)
        {
            LogService.Warn("Tausch-Benachrichtigung konnte nicht gesendet werden: {0}", ex.Message);
        }
    }

    /// <summary>Nach dem Schließen des Dialogs: die Aktion lief schon, nur noch die Woche neu zeigen.</summary>
    public async Task OnSwapDialogClosedAsync(SwapDialogResult? result)
    {
        if (result is not null) await LoadWeekAsync();
    }

    private static string FmtDate(string iso) => DateOnly.Parse(iso).ToString("dd.MM.yyyy");
}
