using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.Views;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Browser-Backend: rendert Dialog-Inhalte als UserControl in ein Overlay-Panel in der
/// MainView (kein OS-Window verfügbar im SingleView-Lifetime). Backdrop blockt Klicks auf
/// den Hauptinhalt; das Result wird über das <c>Closed</c>-Event des ViewModels geliefert.
/// </summary>
public class OverlayDialogService : IDialogService
{
    private readonly Panel _overlay;
    private readonly ContentControl _content;
    private Action? _cancelCurrent;

    public OverlayDialogService(Panel overlay, ContentControl content)
    {
        _overlay = overlay;
        _content = content;
    }

    public Task<EntryDialogResult?> ShowEntryEditorAsync(EntryEditorViewModel vm)
        => ShowAsync<EntryDialogResult>(new EntryEditorView { DataContext = vm },
            h => vm.Closed += h, h => vm.Closed -= h,
            () => vm.CancelCommand.Execute(null));

    public Task<SwapDialogResult?> ShowShiftSwapAsync(ShiftSwapViewModel vm)
        => ShowAsync<SwapDialogResult>(new ShiftSwapView { DataContext = vm },
            h => vm.Closed += h, h => vm.Closed -= h,
            () => vm.CancelCommand.Execute(null));

    public Task<ReplanResult?> ShowReplanAsync(ReplanViewModel vm)
        => ShowAsync<ReplanResult>(new ReplanView { DataContext = vm },
            h => vm.Closed += h, h => vm.Closed -= h,
            () => vm.CancelCommand.Execute(null));

    public Task<string?> ShowDayNoteAsync(DayNoteViewModel vm)
        => ShowAsync<string>(new DayNoteView { DataContext = vm },
            h => vm.Closed += h, h => vm.Closed -= h,
            () => vm.CancelCommand.Execute(null));

    public void CancelActive() => _cancelCurrent?.Invoke();

    private Task<TResult?> ShowAsync<TResult>(
        Control content,
        Action<Action<TResult?>> subscribe,
        Action<Action<TResult?>> unsubscribe,
        Action vmCancel) where TResult : class
    {
        var tcs = new TaskCompletionSource<TResult?>();

        Action<TResult?> handler = null!;
        handler = result =>
        {
            unsubscribe(handler);
            _cancelCurrent = null;
            _content.Content = null;
            _overlay.IsVisible = false;
            tcs.TrySetResult(result);
        };
        subscribe(handler);
        _cancelCurrent = vmCancel;

        _content.Content = content;
        _overlay.IsVisible = true;
        return tcs.Task;
    }
}
