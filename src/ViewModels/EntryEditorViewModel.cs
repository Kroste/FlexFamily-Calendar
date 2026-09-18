using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine wählbare Art der Abwesenheit (Urlaub/Krank/Abwesend).</summary>
public record EntryTypeOption(EntryType Type, string Label);

public enum EntryDialogAction { Save, Delete }

public record EntryDialogResult(EntryDialogAction Action, CalendarEntry Entry, DateOnly RangeStart, DateOnly RangeEnd)
{
    /// <summary>Als Zeitraum speichern: Abwesenheiten immer, alles andere, sobald es mehr als einen Tag spannt.</summary>
    public bool IsSpan => EntryTypeInfo.IsAbsence(Entry.Type) || RangeEnd > RangeStart;
}

/// <summary>
/// Dialog für einen Kalendereintrag, in zwei Modi:
/// <list type="bullet">
/// <item><b>Eintrag</b>: Freitext-Bezeichnung („Arbeit", „Frei", „Sprachschule" …), Uhrzeiten,
/// Kachelfarbe, Notizen. Keine Typ-Auswahl mehr — sie war zu starr, die Bezeichnung ist der Name
/// im Plan. Neue Einträge zählen intern als Arbeit, damit Stundenkonto, Tausch und Freigabe-Regel
/// unverändert weiterlaufen, bis das Stundenkonto umgebaut ist; bestehende behalten ihren Typ.</item>
/// <item><b>Abwesenheit</b>: Urlaub/Krank/Abwesend. Hier hängen Genehmigung, Krankmeldung und
/// Datenschutz-Maskierung dran, deshalb bleibt die Art wählbar.</item>
/// </list>
/// Beide Modi haben Start und Ende mit Datum und Uhrzeit wie im Google-Kalender, dazu
/// „Ganztägig" (bei Abwesenheiten vorbelegt). Verschiebt man den Start, wandert das Ende mit und
/// die Dauer bleibt. Wie daraus gespeichert wird, entscheidet <see cref="EntrySpans.Resolve"/>.
/// Den Modus wählt der Admin beim Anlegen oben im Dialog; ein Klick in die Zelle verrät nicht,
/// was er vorhat. Mitarbeiter, die sich selbst krank oder in Urlaub melden, landen direkt in der
/// Abwesenheit. Beim Bearbeiten steht der Modus durch den Eintrag fest.
/// </summary>
public partial class EntryEditorViewModel : ViewModelBase
{
    private readonly string _entryId;
    private readonly IReadOnlyList<EntryType> _allowedTypes;
    private readonly EntryType _entryType = EntryType.Work;   // Typ im Eintrags-Modus
    private string? _origGroupId;     // bestehende Abwesenheits-Gruppe (zum Aufräumen beim Bearbeiten)
    private DateOnly? _origStart;
    private DateOnly? _origEnd;

    [ObservableProperty] private User? _selectedUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEntryMode))]
    [NotifyPropertyChangedFor(nameof(ShowTimes))]
    [NotifyPropertyChangedFor(nameof(ShowOvernightNote))]
    [NotifyPropertyChangedFor(nameof(TitleLabel))]
    [NotifyPropertyChangedFor(nameof(TitlePlaceholder))]
    [NotifyPropertyChangedFor(nameof(AutoColor))]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private bool _isAbsenceMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoColor))]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private EntryTypeOption? _selectedAbsenceKind;

    /// <summary>Frei gewählte Kachelfarbe (leer = automatisch aus dem Typ).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private string _color = "";

    /// <summary>Schalter „eigene Farbe" — aus heißt: der Eintrag trägt die Farbe seines Typs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private bool _useCustomColor;

    // Bewusst ohne Vorbelegung: ein neuer Eintrag startet mit leeren Zeitfeldern, damit die
    // Uhrzeit direkt getippt werden kann. Save fängt leere Felder über Entry_ErrorNoStart/-NoEnd ab.
    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;
    [ObservableProperty] private DateTimeOffset? _startDate;
    [ObservableProperty] private DateTimeOffset? _endDate;

    /// <summary>Ganztägig: keine Uhrzeit, zählt keine Stunden. Bei Abwesenheiten vorbelegt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTimes))]
    private bool _isAllDay;

    private bool _syncing;   // unterdrückt das Mitwandern des Endes, solange der Dialog selbst setzt
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _errorMessage = "";

    public DateOnly Date { get; }
    public string DateLabel { get; }
    public bool IsEditMode { get; }
    public string HeaderText => Localizer.Instance[IsEditMode ? "Entry_Edit" : "Entry_New"];
    public IReadOnlyList<User> AvailableUsers { get; }

    /// <summary>Bisher verwendete Bezeichnungen — Vorschläge beim Tippen, frei bleibt es trotzdem.</summary>
    public IReadOnlyList<string> TitleSuggestions { get; }

    /// <summary>Urlaub/Krank/Abwesend, soweit erlaubt (bei finalisierter Woche z.B. nur Krank).</summary>
    public IReadOnlyList<EntryTypeOption> AbsenceKinds { get; }

    /// <summary>Gegenstück zu <see cref="IsAbsenceMode"/> für den Umschalter im Dialog.</summary>
    public bool IsEntryMode
    {
        get => !IsAbsenceMode;
        set => IsAbsenceMode = !value;
    }

    /// <summary>Umschalter nur beim Anlegen und nur, wenn beides erlaubt ist.</summary>
    public bool CanSwitchMode { get; }

    /// <summary>Uhrzeitfelder neben den Datumsfeldern — nicht bei ganztägig.</summary>
    public bool ShowTimes => !IsAllDay;

    /// <summary>Der Typ, den der Eintrag beim Speichern bekommt.</summary>
    public EntryType EffectiveType => IsAbsenceMode
        ? SelectedAbsenceKind?.Type ?? EntryType.Absence
        : _entryType;

    public string TitleLabel => Localizer.Instance[IsAbsenceMode ? "Entry_TitleLabel" : "Entry_Name"];
    public string TitlePlaceholder => Localizer.Instance[IsAbsenceMode ? "Entry_TitlePlaceholder" : "Entry_NamePlaceholder"];

    /// <summary>Farbe, die der Eintrag ohne eigene Wahl bekäme.</summary>
    public string AutoColor => EntryColors.ForType(EffectiveType);

    /// <summary>Farbe, die die Kachel am Ende trägt — für die Vorschau im Dialog.</summary>
    public string PreviewColor => UseCustomColor && EntryColors.IsValidHex(Color) ? Color : AutoColor;

    /// <summary>Schriftfarbe auf der Vorschau, nach derselben Regel wie im Plan.</summary>
    public string PreviewForeground => EntryColors.OnTile(PreviewColor);

    /// <summary>Hinweis auf die Stunden-Pauschale — nur noch beim Bearbeiten alter Übernachtungen.</summary>
    public bool ShowOvernightNote => !IsAbsenceMode && _entryType == EntryType.Overnight;

    public string OvernightNote => Localizer.Instance["Entry_OvernightNote"];

    /// <summary>Im Selbst-Antrag (Krank/Urlaub) ist der Benutzer fix → kein Benutzer-Dropdown.</summary>
    public bool CanPickUser { get; }

    public event Action<EntryDialogResult?>? Closed;

    /// <summary>
    /// Neuer Eintrag. canPickUser=false → Person fix (Klick in deren Zeile oder Selbst-Antrag).
    /// allowedTypes=null → alles; nur Abwesenheits-Typen → reiner Abwesenheits-Dialog.
    /// </summary>
    public EntryEditorViewModel(DateOnly date, IReadOnlyList<User> users,
        bool canPickUser = true, IReadOnlyList<EntryType>? allowedTypes = null,
        IReadOnlyList<string>? titleSuggestions = null)
    {
        Date = date;
        DateLabel = date.ToString("D", CultureInfo.CurrentCulture);
        AvailableUsers = users;
        CanPickUser = canPickUser;
        TitleSuggestions = titleSuggestions ?? Array.Empty<string>();
        _allowedTypes = allowedTypes is { Count: > 0 } ? allowedTypes : Enum.GetValues<EntryType>();

        AbsenceKinds = _allowedTypes.Where(EntryTypeInfo.IsAbsence)
            .Select(t => new EntryTypeOption(t, Localizer.Instance[EntryTypeInfo.Key(t)]))
            .ToList();
        var canEntries = _allowedTypes.Any(t => !EntryTypeInfo.IsAbsence(t));
        CanSwitchMode = canEntries && AbsenceKinds.Count > 0;

        _entryId = Guid.NewGuid().ToString();
        IsEditMode = false;
        SelectedUser = users.FirstOrDefault();
        SelectedAbsenceKind = AbsenceKinds.FirstOrDefault();
        IsAbsenceMode = !canEntries;
        IsAllDay = IsAbsenceMode;

        _syncing = true;
        StartDate = ToOffset(date);
        EndDate = ToOffset(date);
        _syncing = false;
    }

    private static DateTimeOffset ToOffset(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue));
    private static DateOnly ToDate(DateTimeOffset d) => DateOnly.FromDateTime(d.Date);

    /// <summary>Bestehenden Eintrag bearbeiten. Der Modus steht durch den Eintrag fest.</summary>
    public EntryEditorViewModel(DateOnly date, IReadOnlyList<User> users, CalendarEntry existing,
        bool canPickUser = true, IReadOnlyList<EntryType>? allowedTypes = null,
        IReadOnlyList<string>? titleSuggestions = null)
        : this(date, users, canPickUser, allowedTypes, titleSuggestions)
    {
        IsEditMode = true;
        CanSwitchMode = false;
        _entryId = existing.Id;
        SelectedUser = users.FirstOrDefault(u => u.Id == existing.UserId) ?? users.FirstOrDefault();

        var isAbsence = EntryTypeInfo.IsAbsence(existing.Type);
        IsAbsenceMode = isAbsence;
        if (isAbsence)
        {
            SelectedAbsenceKind = AbsenceKinds.FirstOrDefault(k => k.Type == existing.Type)
                                  ?? new EntryTypeOption(existing.Type, Localizer.Instance[EntryTypeInfo.Key(existing.Type)]);
        }
        else
        {
            // Bestehende Einträge behalten ihren Typ (Arbeit, Aktivität, alte Übernachtung …).
            _entryType = existing.Type;
        }

        _syncing = true;
        IsAllDay = existing.IsAllDay;
        if (existing.IsAllDay)
        {
            StartTime = null;
            EndTime = null;
        }
        else if (existing.AbsenceGroupId != null)
        {
            // Zeitraum: die Eckzeiten des Ganzen, nicht der Anteil des geklickten Tages.
            StartTime = existing.SpanStartTime ?? existing.StartTime;
            EndTime = existing.SpanEndTime ?? existing.EndTime;
        }
        else
        {
            StartTime = existing.StartTime;
            EndTime = existing.EndTime;
        }

        var start = existing.AbsenceStart ?? date;
        // Nachtschicht: steht als ein Eintrag am Starttag, endet aber am Folgetag.
        var end = existing.AbsenceEnd ?? (existing.CrossesMidnight ? date.AddDays(1) : date);
        StartDate = ToOffset(start);
        EndDate = ToOffset(end);
        _syncing = false;
        // Alt-Einträge ohne Bezeichnung zeigten im Plan ihren Typ („Arbeit"). Den übernehmen, damit
        // die Kachel nach dem Speichern gleich aussieht und das Pflichtfeld nicht leer dasteht.
        Title = !isAbsence && string.IsNullOrWhiteSpace(existing.Title)
            ? Localizer.Instance[EntryTypeInfo.Key(existing.Type)]
            : existing.Title;
        Notes = existing.Notes;
        UseCustomColor = EntryColors.IsValidHex(existing.Color);
        Color = UseCustomColor ? existing.Color : "";

        _origGroupId = existing.AbsenceGroupId;
        _origStart = existing.AbsenceStart;
        _origEnd = existing.AbsenceEnd;
    }

    partial void OnUseCustomColorChanged(bool value)
    {
        // Beim Einschalten mit der Farbe starten, die der Eintrag ohnehin hätte — so verschiebt
        // man von einem sinnvollen Wert aus, statt bei Schwarz zu beginnen.
        if (value && !EntryColors.IsValidHex(Color)) Color = AutoColor;
        else if (!value) Color = "";
    }

    // Der Farbwähler (ColorView) schreibt beim Aufbau seinen Startwert zurück — Grau, weil der
    // Konverter für eine leere Farbe Grau liefert. Das landete als „#808080" im ViewModel, obwohl
    // „eigene Farbe" aus war, und beim Einschalten startete die Vorschau dann grau statt mit der
    // Standardfarbe. Solange der Schalter aus ist, gibt es keine eigene Farbe.
    partial void OnColorChanged(string value)
    {
        if (!UseCustomColor && value.Length > 0) Color = "";
    }

    partial void OnIsAbsenceModeChanged(bool value)
    {
        ErrorMessage = "";
        // Umschalten beim Anlegen: Abwesenheiten sind meist ganztägig, Einträge haben eine Uhrzeit.
        if (!IsEditMode) IsAllDay = value;
    }

    // Start verschieben → Ende wandert mit, die Dauer bleibt (wie im Google-Kalender). Gerechnet
    // wird auf Datum + Uhrzeit, damit ein Start über Mitternacht auch das Enddatum mitnimmt.
    partial void OnStartDateChanged(DateTimeOffset? oldValue, DateTimeOffset? newValue)
    {
        if (_syncing || oldValue is not { } o || newValue is not { } n || EndDate is not { } e) return;
        ShiftEnd(ToDate(o), StartTime, ToDate(n), StartTime, ToDate(e));
    }

    partial void OnStartTimeChanged(TimeSpan? oldValue, TimeSpan? newValue)
    {
        if (_syncing || oldValue is null || newValue is null || StartDate is not { } sd || EndDate is not { } ed) return;
        ShiftEnd(ToDate(sd), oldValue, ToDate(sd), newValue, ToDate(ed));
    }

    private void ShiftEnd(DateOnly oldDate, TimeSpan? oldTime, DateOnly newDate, TimeSpan? newTime, DateOnly endDate)
    {
        // Ganztägig oder noch ohne Uhrzeit: nur das Datum wandert mit.
        var timed = !IsAllDay && oldTime is not null && newTime is not null && EndTime is not null;
        var oldStart = oldDate.ToDateTime(TimeOnly.MinValue) + (timed ? oldTime!.Value : TimeSpan.Zero);
        var newStart = newDate.ToDateTime(TimeOnly.MinValue) + (timed ? newTime!.Value : TimeSpan.Zero);
        var oldEnd = endDate.ToDateTime(TimeOnly.MinValue) + (timed ? EndTime!.Value : TimeSpan.Zero);
        // Nachtschicht mit gleichem Datum eingetippt (20:00–06:00): endet in Wahrheit am Folgetag.
        // Diese Schreibweise bleibt beim Verschieben erhalten, sonst sprünge das Enddatum.
        var overnightSameDate = timed && endDate == oldDate && oldEnd <= oldStart;
        if (overnightSameDate) oldEnd = oldEnd.AddDays(1);
        var newEnd = newStart + (oldEnd - oldStart);

        _syncing = true;
        EndDate = ToOffset(overnightSameDate ? newDate : DateOnly.FromDateTime(newEnd));
        if (timed) EndTime = newEnd.TimeOfDay;
        _syncing = false;
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = "";
        if (SelectedUser == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoUser"]; return; }

        if (IsAbsenceMode && SelectedAbsenceKind == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoType"]; return; }
        // Die Bezeichnung ist der Name im Plan — ohne sie stünde die Kachel namenlos da.
        if (!IsAbsenceMode && string.IsNullOrWhiteSpace(Title)) { ErrorMessage = Localizer.Instance["Entry_ErrorNoName"]; return; }
        if (StartDate == null || EndDate == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoDate"]; return; }

        var (plan, error) = EntrySpans.Resolve(ToDate(StartDate.Value), StartTime,
            ToDate(EndDate.Value), EndTime, IsAllDay, IsAbsenceMode);
        if (plan is null) { ErrorMessage = Localizer.Instance[error!]; return; }

        var entry = new CalendarEntry
        {
            Id = _entryId,
            UserId = SelectedUser.Id,
            UserDisplayName = string.IsNullOrEmpty(SelectedUser.DisplayName) ? SelectedUser.Username : SelectedUser.DisplayName,
            Type = EffectiveType,
            AllDay = plan.AllDay,
            // Einzeleintrag: die Uhrzeiten des Tages. Zeitraum: die Eckzeiten, den Anteil je Tag
            // rechnet EntrySpans.Build.
            StartTime = plan.AllDay ? TimeSpan.Zero : plan.StartTime!.Value,
            EndTime = plan.AllDay ? TimeSpan.Zero : plan.EndTime!.Value,
            SpanStartTime = plan.IsSpan ? plan.StartTime : null,
            SpanEndTime = plan.IsSpan ? plan.EndTime : null,
            Title = Title.Trim(),
            Notes = Notes.Trim(),
            // Nur eine wirklich gewählte, lesbare Farbe wird festgeschrieben — sonst folgt der
            // Eintrag der Farbe seines Typs.
            Color = UseCustomColor && EntryColors.IsValidHex(Color) ? Color : "",
            // bestehende Zeitraum-Gruppe mitführen, damit sie beim Speichern aufgeräumt werden kann
            AbsenceGroupId = _origGroupId,
            AbsenceStart = _origStart,
            AbsenceEnd = _origEnd
        };
        LogService.Debug("Eintrag-Dialog: Speichern ({0}, {1})", entry.TypeLabel, entry.UserDisplayName);
        Closed?.Invoke(new EntryDialogResult(EntryDialogAction.Save, entry, plan.From, plan.To));
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
            Type = EffectiveType,
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
