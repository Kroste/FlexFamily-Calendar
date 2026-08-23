using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Aufbau der Wochenansicht: Tages- und Personenzeilen, projizierte Serientermine,
/// Sichtbarkeits- und Maskierungsregeln sowie Drag-and-Drop von Schichten und Zeilen.
/// </summary>
public partial class CalendarViewModel
{
    private void RebuildDays()
    {
        Days.Clear();
        for (int i = 0; i < 7; i++)
            Days.Add(new CalendarDayViewModel(WeekStart.AddDays(i), this));
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
