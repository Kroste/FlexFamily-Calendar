using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public partial class CalendarDayViewModel : ViewModelBase
{
    private readonly CalendarViewModel _parent;

    public DateOnly Date { get; }
    public string DayName { get; }
    public string DateLabel { get; }
    public bool IsToday { get; }
    public bool CanAddEntry { get; }

    [ObservableProperty] private bool _isFinalized;

    [ObservableProperty] private bool _showHoliday;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string _dayNote = "";

    private bool _isHoliday;
    public string HolidayName { get; private set; } = "";
    public bool HasNote => !string.IsNullOrWhiteSpace(DayNote);

    /// <summary>Admin darf pro Tag einen allgemeinen Hinweis pflegen.</summary>
    public bool CanEditNote => _parent.IsAdmin;

    /// <summary>Nicht-Admins dürfen sich immer krank/Urlaub eintragen (Krank auch bei finalisierter Woche;
    /// Urlaub bei Finalisierung im Editor ausgeblendet).</summary>
    public bool CanRequestAbsence => _parent.CurrentUser.Role != UserRole.Admin;

    /// <summary>Alle echten, gespeicherten Einträge des Tages (für Stunden/Tausch/Regeln) — nicht direkt im Raster.</summary>
    public ObservableCollection<CalendarEntry> Entries { get; } = new();

    /// <summary>Im 24h-Raster gezeigte Einträge: Arbeit + Aktivitäten + Nacht-Fortsetzungen + wiederkehrende Projektionen.</summary>
    public ObservableCollection<CalendarEntry> TimelineEntries { get; } = new();

    /// <summary>Abwesenheiten (Urlaub/Krank/Abwesend) als kompakter Hinweis unter dem Datum.</summary>
    public ObservableCollection<CalendarEntry> AbsenceHints { get; } = new();

    public CalendarDayViewModel(DateOnly date, CalendarViewModel parent)
    {
        Date = date;
        _parent = parent;
        // Wochentagsname kulturabhängig (folgt der aktuellen UI-Sprache)
        DayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
        DateLabel = date.ToString("dd.MM.");
        IsToday = date == DateOnly.FromDateTime(DateTime.Today);
        CanAddEntry = parent.CurrentUser.Role == UserRole.Admin && !parent.IsPersonalView;
    }

    [RelayCommand]
    private void AddEntry()
    {
        LogService.Click(_parent.CurrentUser.Username, $"Eintrag hinzufügen ({Date:dd.MM.yyyy})");
        _parent.RequestAddEntry(Date);
    }

    [RelayCommand]
    private void RequestAbsence() => _parent.RequestSelfAbsence(Date);

    [RelayCommand]
    private void EditNote() => _parent.RequestEditDayNote(Date);

    /// <summary>
    /// Lädt den Tag: <paramref name="day"/> liefert echte Einträge (für Berechnungen);
    /// <paramref name="timeline"/> ist die Raster-Anzeige, <paramref name="absences"/> die Hinweis-Liste.
    /// </summary>
    public void LoadFromModel(CalendarDay day,
        IReadOnlyList<CalendarEntry> timeline, IReadOnlyList<CalendarEntry> absences)
    {
        IsFinalized = day.IsFinalized;
        DayNote = day.Note;

        Entries.Clear();
        foreach (var e in day.Entries.OrderBy(x => x.StartTime))
            Entries.Add(e);

        TimelineEntries.Clear();
        foreach (var e in timeline.OrderBy(x => x.StartTime))
            TimelineEntries.Add(e);

        AbsenceHints.Clear();
        foreach (var e in absences.OrderBy(x => x.UserDisplayName))
            AbsenceHints.Add(e);
    }

    /// <summary>Feiertag (oder keiner) samt Sichtbarkeit setzen; <paramref name="nameKey"/> ist ein i18n-Schlüssel.</summary>
    public void SetHoliday(string? nameKey, bool visible)
    {
        _isHoliday = !string.IsNullOrEmpty(nameKey);
        HolidayName = _isHoliday ? Localizer.Instance[nameKey!] : "";
        ShowHoliday = _isHoliday && visible;
        OnPropertyChanged(nameof(HolidayName));
    }

    /// <summary>Nur die Feiertags-Sichtbarkeit umschalten (Header-Toggle), ohne Neuberechnung.</summary>
    public void SetHolidayVisible(bool visible) => ShowHoliday = _isHoliday && visible;
}
