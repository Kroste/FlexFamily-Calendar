using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Ein Eintrag im Typ-Dropdown. Neben den festen Typen stehen dort die vom Admin gepflegten
/// Aktivitäts-Kategorien direkt drin (<see cref="Activity"/> gesetzt) — vorher musste man erst
/// „Aktivität" und dann in einem zweiten Dropdown die Kategorie wählen, was in der täglichen
/// Planung ein Klick zu viel war und die eigenen Kategorien unsichtbar machte.
/// </summary>
public record EntryTypeOption(EntryType Type, string Label, ActivityType? Activity = null)
{
    /// <summary>Farbe, die ein Eintrag dieser Art im Plan bekommt — für den Punkt im Dropdown.</summary>
    public string PreviewColor => EntryColors.Tile(Type, Activity?.Color);
}

public enum EntryDialogAction { Save, Delete }

public record EntryDialogResult(EntryDialogAction Action, CalendarEntry Entry, DateOnly RangeStart, DateOnly RangeEnd);

public partial class EntryEditorViewModel : ViewModelBase
{
    private readonly string _entryId;
    private readonly IReadOnlyList<ActivityType> _allActivityTypes;
    private readonly IReadOnlyList<EntryType> _allowedTypes;
    private string? _origGroupId;     // bestehende Abwesenheits-Gruppe (zum Aufräumen beim Bearbeiten)
    private DateOnly? _origStart;
    private DateOnly? _origEnd;

    [ObservableProperty] private User? _selectedUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDateRange))]
    [NotifyPropertyChangedFor(nameof(ShowTimes))]
    [NotifyPropertyChangedFor(nameof(ShowOvernightNote))]
    private EntryTypeOption? _selectedType;

    // Bewusst ohne Vorbelegung: ein neuer Eintrag startet mit leeren Zeitfeldern, damit die
    // Uhrzeit direkt getippt werden kann. Eine Vorgabe (früher 08:00–16:00) traf ohnehin selten
    // zu und musste vor jeder Eingabe erst markiert und überschrieben werden. Save fängt die
    // leeren Felder über Entry_ErrorNoStart/-NoEnd ab.
    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;
    [ObservableProperty] private DateTimeOffset? _absenceFrom;
    [ObservableProperty] private DateTimeOffset? _absenceTo;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _errorMessage = "";

    public DateOnly Date { get; }
    public string DateLabel { get; }
    public bool IsEditMode { get; }
    public string HeaderText => Localizer.Instance[IsEditMode ? "Entry_Edit" : "Entry_New"];
    public IReadOnlyList<User> AvailableUsers { get; }

    /// <summary>Feste Typen plus die für die gewählte Person gültigen Kategorien. Wird neu
    /// aufgebaut, wenn die Person wechselt — Kinder haben andere Kategorien als Au-Pairs.</summary>
    public ObservableCollection<EntryTypeOption> EntryTypes { get; } = new();

    /// <summary>Datumsbereich (von–bis) nur bei Abwesenheiten (Urlaub/Krank/Abwesend).</summary>
    public bool ShowDateRange => SelectedType != null && EntryTypeInfo.IsAbsence(SelectedType.Type);

    /// <summary>
    /// Uhrzeiten nur bei Nicht-Abwesenheiten. Eine Abwesenheit spannt ganze Tage — der Kalender
    /// blendet ihre Zeiten ohnehin aus (<see cref="CalendarEntry.ShowsTime"/>) und die
    /// Stundenrechnung überspringt sie. Seit die Felder leer starten, müssen sie hier weg:
    /// sonst verlangte ein Urlaubsantrag eine Uhrzeit, die nirgends eine Bedeutung hat.
    /// </summary>
    public bool ShowTimes => !ShowDateRange;

    /// <summary>Hinweis auf die pauschale Stunden-Gutschrift bei Typ „Übernachtung".</summary>
    public bool ShowOvernightNote => SelectedType?.Type == EntryType.Overnight;

    public string OvernightNote => Localizer.Instance["Entry_OvernightNote"];

    /// <summary>Im Selbst-Antrag (Krank/Urlaub) ist der Benutzer fix → kein Benutzer-Dropdown.</summary>
    public bool CanPickUser { get; }

    public event Action<EntryDialogResult?>? Closed;

    /// <summary>
    /// Neuer Eintrag. canPickUser=false → Selbst-Antrag (Benutzer fix).
    /// allowedTypes=null → alle Typen; sonst nur die erlaubten (z.B. nur Krank bei finalisierter Woche).
    /// </summary>
    public EntryEditorViewModel(DateOnly date, IReadOnlyList<User> users,
        bool canPickUser = true, IReadOnlyList<EntryType>? allowedTypes = null,
        IReadOnlyList<ActivityType>? activityTypes = null)
    {
        Date = date;
        DateLabel = date.ToString("D", CultureInfo.CurrentCulture);
        AvailableUsers = users;
        CanPickUser = canPickUser;
        _allActivityTypes = activityTypes ?? Array.Empty<ActivityType>();

        _allowedTypes = allowedTypes is { Count: > 0 } ? allowedTypes : Enum.GetValues<EntryType>();

        _entryId = Guid.NewGuid().ToString();
        IsEditMode = false;
        SelectedUser = users.FirstOrDefault();   // baut über OnSelectedUserChanged die Typenliste
        // Zweiter Aufruf für den Fall einer leeren Benutzerliste: dort bleibt SelectedUser null,
        // der Wert ändert sich also nicht und die Partial-Methode feuert nie — das Dropdown
        // stünde leer da.
        RebuildTypeOptions();
        var defaultType = canPickUser ? EntryType.Work : _allowedTypes[0];
        SelectedType = EntryTypes.FirstOrDefault(t => t.Type == defaultType && t.Activity is null)
                       ?? EntryTypes.FirstOrDefault();

        var dateOffset = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
        AbsenceFrom = dateOffset;
        AbsenceTo = dateOffset;
    }

    /// <summary>Bestehenden Eintrag bearbeiten.</summary>
    public EntryEditorViewModel(DateOnly date, IReadOnlyList<User> users, CalendarEntry existing,
        bool canPickUser = true, IReadOnlyList<EntryType>? allowedTypes = null,
        IReadOnlyList<ActivityType>? activityTypes = null)
        : this(date, users, canPickUser, allowedTypes, activityTypes)
    {
        IsEditMode = true;
        _entryId = existing.Id;
        SelectedUser = users.FirstOrDefault(u => u.Id == existing.UserId) ?? users.FirstOrDefault();
        // Kategorie-Option bevorzugen, sonst der reine Typ — ein Eintrag, dessen Kategorie
        // inzwischen gelöscht wurde, darf im Dialog nicht ins Leere laufen.
        SelectedType = EntryTypes.FirstOrDefault(t => t.Type == existing.Type && t.Activity?.Id == existing.ActivityTypeId)
                       ?? EntryTypes.FirstOrDefault(t => t.Type == existing.Type && t.Activity is null)
                       ?? EntryTypes.FirstOrDefault(t => t.Type == existing.Type)
                       ?? SelectedType;
        StartTime = existing.StartTime;
        EndTime = existing.EndTime;
        Title = existing.Title;
        Notes = existing.Notes;

        _origGroupId = existing.AbsenceGroupId;
        _origStart = existing.AbsenceStart;
        _origEnd = existing.AbsenceEnd;
        AbsenceFrom = new DateTimeOffset((existing.AbsenceStart ?? date).ToDateTime(TimeOnly.MinValue));
        AbsenceTo = new DateTimeOffset((existing.AbsenceEnd ?? date).ToDateTime(TimeOnly.MinValue));
    }

    partial void OnSelectedUserChanged(User? value) => RebuildTypeOptions();

    /// <summary>
    /// Baut das Typ-Dropdown: erst die festen Typen, dann die Kategorien der gewählten Person.
    /// Die generische Option „Aktivität" bleibt nur übrig, wenn es für diese Person gar keine
    /// Kategorie gibt — sonst stünde sie sinnlos neben „Sprachschule" und „Remise".
    /// </summary>
    private void RebuildTypeOptions()
    {
        var prevType = SelectedType?.Type;
        var prevActivityId = SelectedType?.Activity?.Id;

        var categories = SelectedUser is null
            ? Array.Empty<ActivityType>()
            : _allActivityTypes.Where(t => t.AppliesTo(SelectedUser.Category)).ToArray();
        var activityAllowed = _allowedTypes.Contains(EntryType.Activity);

        EntryTypes.Clear();
        foreach (var t in _allowedTypes)
        {
            if (t == EntryType.Activity && categories.Length > 0 && activityAllowed) continue;
            EntryTypes.Add(new EntryTypeOption(t, Localizer.Instance[EntryTypeInfo.Key(t)]));
        }
        if (activityAllowed)
            foreach (var c in categories)
                EntryTypes.Add(new EntryTypeOption(EntryType.Activity, c.Name, c));

        SelectedType = EntryTypes.FirstOrDefault(t => t.Type == prevType && t.Activity?.Id == prevActivityId)
                       ?? EntryTypes.FirstOrDefault(t => t.Type == prevType)
                       ?? SelectedType;
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = "";
        if (SelectedUser == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoUser"]; return; }
        if (SelectedType == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoType"]; return; }
        if (ShowTimes)
        {
            if (StartTime == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoStart"]; return; }
            if (EndTime == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoEnd"]; return; }
            // EndTime < StartTime ist erlaubt (Schicht über Mitternacht); nur identische Zeiten sind ungültig.
            if (EndTime == StartTime) { ErrorMessage = Localizer.Instance["Entry_ErrorSameTime"]; return; }
        }
        // Custom-Termine brauchen einen Titel — der Eintrag wäre sonst im Plan namenlos.
        if (SelectedType.Type == EntryType.Custom && string.IsNullOrWhiteSpace(Title))
        { ErrorMessage = Localizer.Instance["Entry_ErrorNoTitle"]; return; }

        DateOnly rangeStart, rangeEnd;
        if (ShowDateRange)
        {
            if (AbsenceFrom == null || AbsenceTo == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoDate"]; return; }
            rangeStart = DateOnly.FromDateTime(AbsenceFrom.Value.Date);
            rangeEnd = DateOnly.FromDateTime(AbsenceTo.Value.Date);
            if (rangeEnd < rangeStart) (rangeStart, rangeEnd) = (rangeEnd, rangeStart);
        }
        else
        {
            rangeStart = rangeEnd = Date;
        }

        // Bei Typ "Aktivität" ist die Kategorie die Bezeichnung — wenn der Nutzer kein abweichendes
        // Title-Freifeld nutzt, automatisch den Kategoriename übernehmen. Sonst wäre der Eintrag im
        // Plan ohne Titel ("Aktivität" als generisches Label) und der Server würde ihn ablehnen,
        // solange seine Pflichtfeldprüfung Title/categoryLabel verlangt.
        var effectiveTitle = string.IsNullOrWhiteSpace(Title)
                             && SelectedType.Activity is { } cat
                             && !string.IsNullOrWhiteSpace(cat.Name)
            ? cat.Name
            : Title.Trim();

        var entry = new CalendarEntry
        {
            Id = _entryId,
            UserId = SelectedUser.Id,
            UserDisplayName = string.IsNullOrEmpty(SelectedUser.DisplayName) ? SelectedUser.Username : SelectedUser.DisplayName,
            Type = SelectedType.Type,
            // Bei Abwesenheiten sind die Felder ausgeblendet und damit leer — beim Bearbeiten
            // eines Altbestands stehen dort noch Werte, die bleiben erhalten.
            StartTime = StartTime ?? TimeSpan.Zero,
            EndTime = EndTime ?? TimeSpan.Zero,
            Title = effectiveTitle,
            Notes = Notes.Trim(),
            ActivityTypeId = SelectedType.Activity?.Id,
            // bestehende Abwesenheits-Gruppe mitführen, damit sie beim Speichern aufgeräumt werden kann
            AbsenceGroupId = _origGroupId,
            AbsenceStart = _origStart,
            AbsenceEnd = _origEnd
        };
        LogService.Debug("Eintrag-Dialog: Speichern ({0}, {1})", entry.TypeLabel, entry.UserDisplayName);
        Closed?.Invoke(new EntryDialogResult(EntryDialogAction.Save, entry, rangeStart, rangeEnd));
    }

    [RelayCommand]
    private void Delete()
    {
        if (!IsEditMode) return;
        var entry = new CalendarEntry
        {
            Id = _entryId,
            UserId = SelectedUser?.Id ?? "",
            UserDisplayName = SelectedUser?.DisplayName ?? "",
            Type = SelectedType?.Type ?? EntryType.Work,
            StartTime = StartTime ?? TimeSpan.Zero,
            EndTime = EndTime ?? TimeSpan.Zero,
            Title = Title,
            Notes = Notes,
            AbsenceGroupId = _origGroupId,
            AbsenceStart = _origStart,
            AbsenceEnd = _origEnd
        };
        LogService.Debug("Eintrag-Dialog: Löschen ({0})", entry.TypeLabel);
        Closed?.Invoke(new EntryDialogResult(EntryDialogAction.Delete, entry, _origStart ?? Date, _origEnd ?? Date));
    }

    [RelayCommand]
    private void Cancel()
    {
        LogService.Debug("Eintrag-Dialog abgebrochen");
        Closed?.Invoke(null);
    }
}
