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
    Task<string?> ShowDayNoteAsync(DayNoteViewModel vm);
}
