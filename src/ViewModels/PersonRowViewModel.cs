using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Personenzeile in der tabellarischen Plansicht (Name links, 7 Tageszellen rechts).</summary>
public class PersonRowViewModel
{
    public string Name { get; }
    public string Color { get; }
    public string CategoryLabel { get; }
    public bool IsCurrentUser { get; }
    public IReadOnlyList<PersonDayCellViewModel> Cells { get; }

    public PersonRowViewModel(string name, string color, string categoryLabel,
        bool isCurrentUser, IReadOnlyList<PersonDayCellViewModel> cells)
    {
        Name = name;
        Color = color;
        CategoryLabel = categoryLabel;
        IsCurrentUser = isCurrentUser;
        Cells = cells;
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
