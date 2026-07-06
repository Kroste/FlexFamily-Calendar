using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Plattform-abstrahiertes Öffnen modaler Dialoge.
/// Desktop nutzt <c>Window.ShowDialog</c>; Browser (SingleView-Lifetime, kein Window) rendert
/// denselben UserControl-Inhalt in ein Overlay-Panel des MainView.
/// </summary>
public interface IDialogService
{
    Task<EntryDialogResult?> ShowEntryEditorAsync(EntryEditorViewModel vm);
    Task<SwapDialogResult?> ShowShiftSwapAsync(ShiftSwapViewModel vm);
    Task<ReplanResult?> ShowReplanAsync(ReplanViewModel vm);
    Task<DayNoteResult?> ShowDayNoteAsync(DayNoteViewModel vm);
    Task<UserEditorResult?> ShowUserEditorAsync(UserEditorViewModel vm);
    Task<NotificationResult?> ShowNotificationsAsync(NotificationsViewModel vm);
    Task ShowHoursAccountAsync(HoursAccountViewModel vm);
    Task ShowMonthOverviewAsync(MonthOverviewViewModel vm);
    Task ShowAdminAsync(AdminViewModel vm);
    Task<MoveCopyResult?> ShowMoveCopyAsync(MoveCopyViewModel vm);
    Task<IReadOnlyList<string>?> ShowMailAsync(MailViewModel vm);
    Task<UpdateDialogAction?> ShowUpdateAsync(UpdateViewModel vm);
    Task<IReadOnlyList<RecurrenceSkip>?> ShowRecurrencePauseAsync(RecurrencePauseViewModel vm);
    Task ShowAiPlannerAsync(AiPlannerViewModel vm);
    Task<ConnectionSettingsResult?> ShowConnectionSettingsAsync(ConnectionSettingsViewModel vm);

    Task ShowInfoAsync(InfoViewModel vm);

    /// <summary>
    /// Abbruch des aktuell offenen Dialogs (ESC/Backdrop-Klick im Overlay-Backend).
    /// Desktop-Backend ist No-op — Avalonia-Windows fangen ESC und Außenklicks selbst ab.
    /// </summary>
    void CancelActive();
}
