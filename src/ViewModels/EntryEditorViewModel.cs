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

public record EntryDialogResult(EntryDialogAction Action, CalendarEntry Entry, DateOnly RangeStart, DateOnly RangeEnd);

/// <summary>
/// Dialog für einen Kalendereintrag, in zwei Modi:
/// <list type="bullet">
/// <item><b>Eintrag</b>: Freitext-Bezeichnung („Arbeit", „Frei", „Sprachschule" …), Uhrzeiten,
/// Kachelfarbe, Notizen. Keine Typ-Auswahl mehr — sie war zu starr, die Bezeichnung ist der Name
/// im Plan. Neue Einträge zählen intern als Arbeit, damit Stundenkonto, Tausch und Freigabe-Regel
/// unverändert weiterlaufen, bis das Stundenkonto umgebaut ist; bestehende behalten ihren Typ.</item>
/// <item><b>Abwesenheit</b>: Urlaub/Krank/Abwesend über einen Datumsbereich, ohne Uhrzeit. Hier
/// hängen Genehmigung, Krankmeldung und Datenschutz-Maskierung dran, deshalb bleibt die Art wählbar.</item>
/// </list>
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
    [NotifyPropertyChangedFor(nameof(ShowDateRange))]
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

    public bool ShowDateRange => IsAbsenceMode;

    /// <summary>Uhrzeiten nur im Eintrags-Modus — eine Abwesenheit spannt ganze Tage.</summary>
    public bool ShowTimes => !IsAbsenceMode;

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

        var dateOffset = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
        AbsenceFrom = dateOffset;
        AbsenceTo = dateOffset;
    }

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

        StartTime = existing.StartTime;
        EndTime = existing.EndTime;
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
        AbsenceFrom = new DateTimeOffset((existing.AbsenceStart ?? date).ToDateTime(TimeOnly.MinValue));
        AbsenceTo = new DateTimeOffset((existing.AbsenceEnd ?? date).ToDateTime(TimeOnly.MinValue));
    }

    partial void OnUseCustomColorChanged(bool value)
    {
        // Beim Einschalten mit der Farbe starten, die der Eintrag ohnehin hätte — so verschiebt
        // man von einem sinnvollen Wert aus, statt bei Schwarz zu beginnen.
        if (value && !EntryColors.IsValidHex(Color)) Color = AutoColor;
        else if (!value) Color = "";
    }

    partial void OnIsAbsenceModeChanged(bool value) => ErrorMessage = "";

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = "";
        if (SelectedUser == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoUser"]; return; }

        DateOnly rangeStart, rangeEnd;
        if (IsAbsenceMode)
        {
            if (SelectedAbsenceKind == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoType"]; return; }
            if (AbsenceFrom == null || AbsenceTo == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoDate"]; return; }
            rangeStart = DateOnly.FromDateTime(AbsenceFrom.Value.Date);
            rangeEnd = DateOnly.FromDateTime(AbsenceTo.Value.Date);
            if (rangeEnd < rangeStart) (rangeStart, rangeEnd) = (rangeEnd, rangeStart);
        }
        else
        {
            // Die Bezeichnung ist der Name im Plan — ohne sie stünde die Kachel namenlos da.
            if (string.IsNullOrWhiteSpace(Title)) { ErrorMessage = Localizer.Instance["Entry_ErrorNoName"]; return; }
            if (StartTime == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoStart"]; return; }
            if (EndTime == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoEnd"]; return; }
            // EndTime < StartTime ist erlaubt (Schicht über Mitternacht); nur identische Zeiten sind ungültig.
            if (EndTime == StartTime) { ErrorMessage = Localizer.Instance["Entry_ErrorSameTime"]; return; }
            rangeStart = rangeEnd = Date;
        }

        var entry = new CalendarEntry
        {
            Id = _entryId,
            UserId = SelectedUser.Id,
            UserDisplayName = string.IsNullOrEmpty(SelectedUser.DisplayName) ? SelectedUser.Username : SelectedUser.DisplayName,
            Type = EffectiveType,
            // Bei Abwesenheiten sind die Felder ausgeblendet und damit leer — beim Bearbeiten
            // eines Altbestands stehen dort noch Werte, die bleiben erhalten.
            StartTime = StartTime ?? TimeSpan.Zero,
            EndTime = EndTime ?? TimeSpan.Zero,
            Title = Title.Trim(),
            Notes = Notes.Trim(),
            // Nur eine wirklich gewählte, lesbare Farbe wird festgeschrieben — sonst folgt der
            // Eintrag der Farbe seines Typs.
            Color = UseCustomColor && EntryColors.IsValidHex(Color) ? Color : "",
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
