using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Einträge und Tagesnotizen: anlegen, bearbeiten, aktivieren, Abwesenheiten über
/// mehrere Tage sowie das Pausieren wiederkehrender Aktivitäten.
/// </summary>
public partial class CalendarViewModel
{
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
}
