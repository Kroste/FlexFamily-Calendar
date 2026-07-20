using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Personenzeile in der tabellarischen Plansicht (Name links, 7 Tageszellen rechts).</summary>
public class PersonRowViewModel
{
    public string UserId { get; }
    public string Name { get; }
    public string Color { get; }
    public string CategoryLabel { get; }
    public bool IsCurrentUser { get; }
    public IReadOnlyList<PersonDayCellViewModel> Cells { get; }
    /// <summary>Klick auf den Personennamen schaltet die Sicht in deren Perspektive (Admin-only).</summary>
    public IRelayCommand? ImpersonateCommand { get; }
    public bool CanImpersonate => ImpersonateCommand is not null;
    /// <summary>Zeigt, dass der Admin diese Zeile per Drag&amp;Drop verschieben darf (Sortier-Griff sichtbar).</summary>
    public bool CanReorder { get; }

    public PersonRowViewModel(string userId, string name, string color, string categoryLabel,
        bool isCurrentUser, IReadOnlyList<PersonDayCellViewModel> cells, IRelayCommand? impersonateCommand = null,
        bool canReorder = false)
    {
        UserId = userId;
        Name = name;
        Color = color;
        CategoryLabel = categoryLabel;
        IsCurrentUser = isCurrentUser;
        Cells = cells;
        ImpersonateCommand = impersonateCommand;
        CanReorder = canReorder;
    }
}

/// <summary>Eine Zelle (Person × Tag) mit den Einträgen dieser Person an diesem Tag.</summary>
public class PersonDayCellViewModel
{
    public DateOnly Date { get; }
    public User Person { get; }
    public IReadOnlyList<CalendarEntry> Entries { get; }
    public bool CanAdd { get; }
    public bool IsToday { get; }

    public bool IsEmpty => Entries.Count == 0;
    public bool ShowAddHint => CanAdd && IsEmpty;
    /// <summary>Kleiner Plus-Button rechts unten in der Zelle, wenn schon Einträge da sind.</summary>
    public bool ShowAddMoreButton => CanAdd && !IsEmpty;

    public PersonDayCellViewModel(DateOnly date, User person,
        IReadOnlyList<CalendarEntry> entries, bool canAdd, bool isToday)
    {
        Date = date;
        Person = person;
        Entries = entries;
        CanAdd = canAdd;
        IsToday = isToday;
    }
}
