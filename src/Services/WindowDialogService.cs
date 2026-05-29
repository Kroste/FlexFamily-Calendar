using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.Views;

namespace FlexFamilyCalendar.Services;

/// <summary>Desktop-Backend: hängt Dialoge als Avalonia-Window an das MainWindow.</summary>
public class WindowDialogService : IDialogService
{
    private readonly Window _owner;

    public WindowDialogService(Window owner) => _owner = owner;

    public Task<EntryDialogResult?> ShowEntryEditorAsync(EntryEditorViewModel vm)
        => new EntryEditorDialog { DataContext = vm }.ShowDialog<EntryDialogResult?>(_owner);

    public Task<SwapDialogResult?> ShowShiftSwapAsync(ShiftSwapViewModel vm)
        => new ShiftSwapDialog { DataContext = vm }.ShowDialog<SwapDialogResult?>(_owner);

    public Task<ReplanResult?> ShowReplanAsync(ReplanViewModel vm)
        => new ReplanDialog { DataContext = vm }.ShowDialog<ReplanResult?>(_owner);

    public Task<string?> ShowDayNoteAsync(DayNoteViewModel vm)
        => new DayNoteDialog { DataContext = vm }.ShowDialog<string?>(_owner);

    public Task<UserEditorResult?> ShowUserEditorAsync(UserEditorViewModel vm)
        => new UserEditorDialog { DataContext = vm }.ShowDialog<UserEditorResult?>(_owner);

    public Task<NotificationResult?> ShowNotificationsAsync(NotificationsViewModel vm)
        => new NotificationsDialog { DataContext = vm }.ShowDialog<NotificationResult?>(_owner);

    public Task ShowHoursAccountAsync(HoursAccountViewModel vm)
        => new HoursAccountDialog { DataContext = vm }.ShowDialog(_owner);

    public Task ShowMonthOverviewAsync(MonthOverviewViewModel vm)
        => new MonthOverviewDialog { DataContext = vm }.ShowDialog(_owner);

    public void CancelActive() { }   // Window fängt ESC/Außenklick selbst ab
}
