using Avalonia.Controls;
using FlexFamilyCalendar.Models;
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

    public Task<DayNoteResult?> ShowDayNoteAsync(DayNoteViewModel vm)
        => new DayNoteDialog { DataContext = vm }.ShowDialog<DayNoteResult?>(_owner);

    public Task<UserEditorResult?> ShowUserEditorAsync(UserEditorViewModel vm)
        => new UserEditorDialog { DataContext = vm }.ShowDialog<UserEditorResult?>(_owner);

    public Task<NotificationResult?> ShowNotificationsAsync(NotificationsViewModel vm)
        => new NotificationsDialog { DataContext = vm }.ShowDialog<NotificationResult?>(_owner);

    public Task ShowHoursAccountAsync(HoursAccountViewModel vm)
        => new HoursAccountDialog { DataContext = vm }.ShowDialog(_owner);

    public Task ShowMonthOverviewAsync(MonthOverviewViewModel vm)
        => new MonthOverviewDialog { DataContext = vm }.ShowDialog(_owner);

    public Task ShowAdminAsync(AdminViewModel vm)
        => new AdminDialog { DataContext = vm }.ShowDialog(_owner);

    public Task<MoveCopyResult?> ShowMoveCopyAsync(MoveCopyViewModel vm)
        => new MoveCopyDialog { DataContext = vm }.ShowDialog<MoveCopyResult?>(_owner);

    public Task<IReadOnlyList<string>?> ShowMailAsync(MailViewModel vm)
        => new MailDialog { DataContext = vm }.ShowDialog<IReadOnlyList<string>?>(_owner);

    public Task<UpdateDialogAction?> ShowUpdateAsync(UpdateViewModel vm)
        => new UpdateDialog { DataContext = vm }.ShowDialog<UpdateDialogAction?>(_owner);

    public Task<IReadOnlyList<RecurrenceSkip>?> ShowRecurrencePauseAsync(RecurrencePauseViewModel vm)
        => new RecurrencePauseDialog { DataContext = vm }.ShowDialog<IReadOnlyList<RecurrenceSkip>?>(_owner);

    public Task ShowAiPlannerAsync(AiPlannerViewModel vm)
        => new AiPlannerDialog { DataContext = vm }.ShowDialog(_owner);

    public Task<ConnectionSettingsResult?> ShowConnectionSettingsAsync(ConnectionSettingsViewModel vm)
        => new ConnectionSettingsDialog { DataContext = vm }.ShowDialog<ConnectionSettingsResult?>(_owner);

    public Task ShowInfoAsync(InfoViewModel vm)
        => new InfoDialog { DataContext = vm }.ShowDialog(_owner);

    public async Task<bool> ShowOnboardingAsync(OnboardingViewModel vm)
    {
        await new OnboardingDialog { DataContext = vm }.ShowDialog(_owner);
        return vm.CompletedFully;
    }

    public void CancelActive() { }   // Window fängt ESC/Außenklick selbst ab
}
