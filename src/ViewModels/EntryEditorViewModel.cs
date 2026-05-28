using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public record EntryTypeOption(EntryType Type, string Label);

public enum EntryDialogAction { Save, Delete }

public record EntryDialogResult(EntryDialogAction Action, CalendarEntry Entry);

public partial class EntryEditorViewModel : ViewModelBase
{
    private readonly string _entryId;
    private readonly IReadOnlyList<ActivityType> _allActivityTypes;

    [ObservableProperty] private User? _selectedUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowActivityType))]
    private EntryTypeOption? _selectedType;

    [ObservableProperty] private ActivityType? _selectedActivityType;
    [ObservableProperty] private TimeSpan? _startTime = TimeSpan.FromHours(8);
    [ObservableProperty] private TimeSpan? _endTime = TimeSpan.FromHours(16);
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _errorMessage = "";

    public DateOnly Date { get; }
    public string DateLabel { get; }
    public bool IsEditMode { get; }
    public string HeaderText => Localizer.Instance[IsEditMode ? "Entry_Edit" : "Entry_New"];
    public IReadOnlyList<User> AvailableUsers { get; }
    public IReadOnlyList<EntryTypeOption> EntryTypes { get; }

    /// <summary>Konfigurierbare Aktivitäts-Kategorien, gefiltert nach der Rolle der gewählten Person.</summary>
    public ObservableCollection<ActivityType> AvailableActivityTypes { get; } = new();

    /// <summary>Kategorie-Auswahl nur bei Typ „Aktivität".</summary>
    public bool ShowActivityType => SelectedType?.Type == EntryType.Activity;

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

        var types = allowedTypes is { Count: > 0 } ? allowedTypes : Enum.GetValues<EntryType>();
        EntryTypes = types.Select(t => new EntryTypeOption(t, Localizer.Instance[EntryTypeInfo.Key(t)])).ToList();

        _entryId = Guid.NewGuid().ToString();
        IsEditMode = false;
        SelectedUser = users.FirstOrDefault();
        var defaultType = canPickUser ? EntryType.Work : types[0];
        SelectedType = EntryTypes.FirstOrDefault(t => t.Type == defaultType) ?? EntryTypes.FirstOrDefault();
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
        SelectedType = EntryTypes.FirstOrDefault(t => t.Type == existing.Type) ?? SelectedType;
        StartTime = existing.StartTime;
        EndTime = existing.EndTime;
        Title = existing.Title;
        Notes = existing.Notes;
        SelectedActivityType = AvailableActivityTypes.FirstOrDefault(t => t.Id == existing.ActivityTypeId);
    }

    partial void OnSelectedUserChanged(User? value) => RefreshActivityTypes();

    private void RefreshActivityTypes()
    {
        var prevId = SelectedActivityType?.Id;
        AvailableActivityTypes.Clear();
        if (SelectedUser != null)
            foreach (var t in _allActivityTypes.Where(t => t.AppliesTo(SelectedUser.Category)))
                AvailableActivityTypes.Add(t);
        SelectedActivityType = AvailableActivityTypes.FirstOrDefault(t => t.Id == prevId)
                               ?? AvailableActivityTypes.FirstOrDefault();
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = "";
        if (SelectedUser == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoUser"]; return; }
        if (SelectedType == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoType"]; return; }
        if (StartTime == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoStart"]; return; }
        if (EndTime == null) { ErrorMessage = Localizer.Instance["Entry_ErrorNoEnd"]; return; }
        if (EndTime <= StartTime) { ErrorMessage = Localizer.Instance["Entry_ErrorEndBeforeStart"]; return; }

        var entry = new CalendarEntry
        {
            Id = _entryId,
            UserId = SelectedUser.Id,
            UserDisplayName = string.IsNullOrEmpty(SelectedUser.DisplayName) ? SelectedUser.Username : SelectedUser.DisplayName,
            Type = SelectedType.Type,
            StartTime = StartTime.Value,
            EndTime = EndTime.Value,
            Title = Title.Trim(),
            Notes = Notes.Trim(),
            ActivityTypeId = ShowActivityType ? SelectedActivityType?.Id : null
        };
        LogService.Debug("Eintrag-Dialog: Speichern ({0}, {1})", entry.TypeLabel, entry.UserDisplayName);
        Closed?.Invoke(new EntryDialogResult(EntryDialogAction.Save, entry));
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
            Notes = Notes
        };
        LogService.Debug("Eintrag-Dialog: Löschen ({0})", entry.TypeLabel);
        Closed?.Invoke(new EntryDialogResult(EntryDialogAction.Delete, entry));
    }

    [RelayCommand]
    private void Cancel()
    {
        LogService.Debug("Eintrag-Dialog abgebrochen");
        Closed?.Invoke(null);
    }
}
