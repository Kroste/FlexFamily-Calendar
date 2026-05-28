using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Verwaltung wiederkehrender Aktivitäten (Master-Detail in einem Dialog, nur Admin).</summary>
public partial class RecurringActivityManagementViewModel : ViewModelBase
{
    private readonly StorageService _storage;
    private List<RecurringActivity> _all = new();
    private List<User> _users = new();
    private List<ActivityType> _activityTypes = new();

    public ObservableCollection<RecurringActivity> Activities { get; } = new();
    public ObservableCollection<User> AvailableUsers { get; } = new();
    public ObservableCollection<ActivityType> AvailableActivityTypes { get; } = new();

    [ObservableProperty] private RecurringActivity? _selectedActivity;
    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private ActivityType? _selectedActivityType;
    [ObservableProperty] private string _editTitle = "";
    [ObservableProperty] private TimeSpan? _startTime = TimeSpan.FromHours(16);
    [ObservableProperty] private TimeSpan? _endTime = TimeSpan.FromHours(17);
    [ObservableProperty] private bool _mon;
    [ObservableProperty] private bool _tue;
    [ObservableProperty] private bool _wed;
    [ObservableProperty] private bool _thu;
    [ObservableProperty] private bool _fri;
    [ObservableProperty] private bool _sat;
    [ObservableProperty] private bool _sun;
    [ObservableProperty] private bool _skipOnHolidays = true;
    [ObservableProperty] private string _errorMessage = "";

    public RecurringActivityManagementViewModel(StorageService storage)
    {
        _storage = storage;
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _users = await _storage.LoadUsersAsync();
        _activityTypes = await _storage.LoadActivityTypesAsync();
        _all = await _storage.LoadRecurringActivitiesAsync();

        AvailableUsers.Clear();
        foreach (var u in _users.OrderBy(u => string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName))
            AvailableUsers.Add(u);

        Activities.Clear();
        foreach (var a in _all.OrderBy(a => a.UserDisplayName).ThenBy(a => a.StartTime))
            Activities.Add(a);
    }

    partial void OnSelectedUserChanged(User? value) => RefreshActivityTypes();

    private void RefreshActivityTypes()
    {
        var prevId = SelectedActivityType?.Id;
        AvailableActivityTypes.Clear();
        if (SelectedUser != null)
            foreach (var t in _activityTypes.Where(t => t.AppliesTo(SelectedUser.Category)))
                AvailableActivityTypes.Add(t);
        SelectedActivityType = AvailableActivityTypes.FirstOrDefault(t => t.Id == prevId);
    }

    partial void OnSelectedActivityChanged(RecurringActivity? value)
    {
        if (value == null) return;
        ErrorMessage = "";
        SelectedUser = AvailableUsers.FirstOrDefault(u => u.Id == value.UserId);
        EditTitle = value.Title;
        StartTime = value.StartTime;
        EndTime = value.EndTime;
        Mon = value.Weekdays.Contains(DayOfWeek.Monday);
        Tue = value.Weekdays.Contains(DayOfWeek.Tuesday);
        Wed = value.Weekdays.Contains(DayOfWeek.Wednesday);
        Thu = value.Weekdays.Contains(DayOfWeek.Thursday);
        Fri = value.Weekdays.Contains(DayOfWeek.Friday);
        Sat = value.Weekdays.Contains(DayOfWeek.Saturday);
        Sun = value.Weekdays.Contains(DayOfWeek.Sunday);
        SkipOnHolidays = value.SkipOnHolidays;
        SelectedActivityType = AvailableActivityTypes.FirstOrDefault(t => t.Id == value.ActivityTypeId);
    }

    private List<DayOfWeek> CollectWeekdays()
    {
        var list = new List<DayOfWeek>();
        if (Mon) list.Add(DayOfWeek.Monday);
        if (Tue) list.Add(DayOfWeek.Tuesday);
        if (Wed) list.Add(DayOfWeek.Wednesday);
        if (Thu) list.Add(DayOfWeek.Thursday);
        if (Fri) list.Add(DayOfWeek.Friday);
        if (Sat) list.Add(DayOfWeek.Saturday);
        if (Sun) list.Add(DayOfWeek.Sunday);
        return list;
    }

    [RelayCommand]
    private void New()
    {
        SelectedActivity = null;
        SelectedUser = AvailableUsers.FirstOrDefault();
        EditTitle = "";
        StartTime = TimeSpan.FromHours(16);
        EndTime = TimeSpan.FromHours(17);
        Mon = Tue = Wed = Thu = Fri = Sat = Sun = false;
        SkipOnHolidays = true;
        ErrorMessage = "";
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = "";
        if (SelectedUser == null) { ErrorMessage = Localizer.Instance["Recur_ErrorNoUser"]; return; }
        if (string.IsNullOrWhiteSpace(EditTitle)) { ErrorMessage = Localizer.Instance["Recur_ErrorNoTitle"]; return; }
        if (StartTime == null || EndTime == null) { ErrorMessage = Localizer.Instance["Recur_ErrorNoTime"]; return; }
        if (EndTime <= StartTime) { ErrorMessage = Localizer.Instance["Recur_ErrorEndBeforeStart"]; return; }
        var weekdays = CollectWeekdays();
        if (weekdays.Count == 0) { ErrorMessage = Localizer.Instance["Recur_ErrorNoWeekday"]; return; }

        var name = string.IsNullOrEmpty(SelectedUser.DisplayName) ? SelectedUser.Username : SelectedUser.DisplayName;

        if (SelectedActivity == null)
        {
            _all.Add(new RecurringActivity
            {
                UserId = SelectedUser.Id,
                UserDisplayName = name,
                Title = EditTitle.Trim(),
                ActivityTypeId = SelectedActivityType?.Id,
                StartTime = StartTime.Value,
                EndTime = EndTime.Value,
                Weekdays = weekdays,
                SkipOnHolidays = SkipOnHolidays
            });
        }
        else
        {
            SelectedActivity.UserId = SelectedUser.Id;
            SelectedActivity.UserDisplayName = name;
            SelectedActivity.Title = EditTitle.Trim();
            SelectedActivity.ActivityTypeId = SelectedActivityType?.Id;
            SelectedActivity.StartTime = StartTime.Value;
            SelectedActivity.EndTime = EndTime.Value;
            SelectedActivity.Weekdays = weekdays;
            SelectedActivity.SkipOnHolidays = SkipOnHolidays;
        }

        await _storage.SaveRecurringActivitiesAsync(_all);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedActivity == null) return;
        _all.RemoveAll(a => a.Id == SelectedActivity.Id);
        await _storage.SaveRecurringActivitiesAsync(_all);
        New();
        await ReloadAsync();
    }
}
