using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ObservableCollection<CalendarEntry> Entries { get; } = new();

    public CalendarDayViewModel(DateOnly date, CalendarViewModel parent)
    {
        Date = date;
        _parent = parent;
        // Wochentagsname kulturabhängig (folgt der aktuellen UI-Sprache)
        DayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
        DateLabel = date.ToString("dd.MM.");
        IsToday = date == DateOnly.FromDateTime(DateTime.Today);
        CanAddEntry = parent.CurrentUser.Role == UserRole.Admin;
    }

    [RelayCommand]
    private void AddEntry()
    {
        LogService.Click(_parent.CurrentUser.Username, $"Eintrag hinzufügen ({Date:dd.MM.yyyy})");
        _parent.RequestAddEntry(Date);
    }

    public void LoadFromModel(CalendarDay day)
    {
        IsFinalized = day.IsFinalized;
        Entries.Clear();
        foreach (var e in day.Entries.OrderBy(x => x.StartTime))
            Entries.Add(e);
    }
}
