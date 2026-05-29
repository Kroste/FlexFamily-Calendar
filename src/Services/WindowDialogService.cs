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
    {
        var dialog = new EntryEditorDialog { DataContext = vm };
        return dialog.ShowDialog<EntryDialogResult?>(_owner);
    }
}
