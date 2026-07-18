using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels.Mobile;

public enum MobileTab { Calendar, Sick, Vacation, Swap }

/// <summary>
/// Wrapper um <see cref="MainWindowViewModel"/> für den Android-Head. Reduziert die Fläche
/// bewusst auf: Anmelden, Kalender (nur anzeigen), Krank melden, Urlaub melden, Tausch —
/// alles andere (Admin, PDF, Mail, KI, Stundenkonto-Editor, Profil-Editor, Monatsübersicht,
/// wiederkehrende Aktivitäten) bleibt weg.
/// </summary>
public partial class MobileMainViewModel : ObservableObject
{
    public MainWindowViewModel Main { get; }
    private readonly IStorageService _storage;
    private readonly AuthService _auth;

    [ObservableProperty] private MobileTab _activeTab = MobileTab.Calendar;
    [ObservableProperty] private MobileAbsenceViewModel? _sickVm;
    [ObservableProperty] private MobileAbsenceViewModel? _vacationVm;
    [ObservableProperty] private MobileSwapViewModel? _swapVm;

    public bool IsCalendarActive => ActiveTab == MobileTab.Calendar;
    public bool IsSickActive => ActiveTab == MobileTab.Sick;
    public bool IsVacationActive => ActiveTab == MobileTab.Vacation;
    public bool IsSwapActive => ActiveTab == MobileTab.Swap;

    private readonly NotificationService _notifications;

    public MobileMainViewModel(MainWindowViewModel main, IStorageService storage, AuthService auth,
        NotificationService notifications)
    {
        Main = main;
        _storage = storage;
        _auth = auth;
        _notifications = notifications;

        Main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.IsLoggedIn) or nameof(MainWindowViewModel.CalendarVm))
                RebuildTabViewModels();
        };
        RebuildTabViewModels();
    }

    private void RebuildTabViewModels()
    {
        if (Main.CalendarVm?.CurrentUser is { } user)
        {
            SickVm = new MobileAbsenceViewModel(_storage, user, EntryType.SickLeave);
            VacationVm = new MobileAbsenceViewModel(_storage, user, EntryType.Vacation);
            SwapVm = new MobileSwapViewModel(_storage, _notifications, user);
        }
        else
        {
            SickVm = null;
            VacationVm = null;
            SwapVm = null;
        }
    }

    partial void OnActiveTabChanged(MobileTab value)
    {
        OnPropertyChanged(nameof(IsCalendarActive));
        OnPropertyChanged(nameof(IsSickActive));
        OnPropertyChanged(nameof(IsVacationActive));
        OnPropertyChanged(nameof(IsSwapActive));
    }

    [RelayCommand] private void ShowCalendar() => ActiveTab = MobileTab.Calendar;
    [RelayCommand] private void ShowSick() => ActiveTab = MobileTab.Sick;
    [RelayCommand] private void ShowVacation() => ActiveTab = MobileTab.Vacation;
    [RelayCommand] private void ShowSwap() => ActiveTab = MobileTab.Swap;
}
