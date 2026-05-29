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

    public OverlayDialogService(Panel overlay, ContentControl content)
    {
        _overlay = overlay;
        _content = content;
    }

    public Task<EntryDialogResult?> ShowEntryEditorAsync(EntryEditorViewModel vm)
    {
        var tcs = new TaskCompletionSource<EntryDialogResult?>();

        void OnClosed(EntryDialogResult? result)
        {
            vm.Closed -= OnClosed;
            _content.Content = null;
            _overlay.IsVisible = false;
            tcs.TrySetResult(result);
        }
        vm.Closed += OnClosed;

        _content.Content = new EntryEditorView { DataContext = vm };
        _overlay.IsVisible = true;

        return tcs.Task;
    }
}
