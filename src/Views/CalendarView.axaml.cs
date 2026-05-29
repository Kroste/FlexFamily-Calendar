using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class CalendarView : UserControl
{
    private CalendarViewModel? _vm;

    // Avalonia 12 verlangt DataFormat-Instanzen für DragDrop-Payloads. In-Process-Format reicht,
    // weil wir die Daten nur zwischen Controls in derselben App reichen.
    private static readonly DataFormat<string> EntryIdFormat =
        DataFormat.CreateInProcessFormat<string>("ffc-entry-id");
    private static readonly DataFormat<string> SourceDateFormat =
        DataFormat.CreateInProcessFormat<string>("ffc-source-date");

    private static readonly IBrush DropTargetBrush =
        new SolidColorBrush(Color.FromArgb(0x55, 0x2E, 0x86, 0xC1));

    public CalendarView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
        {
            _vm.EntryDialogRequested -= OnEntryDialogRequested;
            _vm.SwapDialogRequested -= OnSwapDialogRequested;
            _vm.ReplanDialogRequested -= OnReplanDialogRequested;
            _vm.DayNoteDialogRequested -= OnDayNoteDialogRequested;
            _vm.ExportPdfRequested -= OnExportPdfRequested;
            _vm.MailDialogRequested -= OnMailDialogRequested;
        }

        _vm = DataContext as CalendarViewModel;

        if (_vm != null)
        {
            _vm.EntryDialogRequested += OnEntryDialogRequested;
            _vm.SwapDialogRequested += OnSwapDialogRequested;
            _vm.ReplanDialogRequested += OnReplanDialogRequested;
            _vm.DayNoteDialogRequested += OnDayNoteDialogRequested;
            _vm.ExportPdfRequested += OnExportPdfRequested;
            _vm.MailDialogRequested += OnMailDialogRequested;
        }
    }

    private async void OnMailDialogRequested(MailViewModel vm)
    {
        if (_vm == null) return;
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var dialog = new MailDialog { DataContext = vm };
            var result = await dialog.ShowDialog<IReadOnlyList<string>?>(owner);

            if (result is { Count: > 0 })
                await _vm.SendPlanMailAsync(result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Mail-Dialog", ex);
        }
    }

    private async void OnExportPdfRequested()
    {
        if (_vm == null) return;
        try
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = _vm.ExportFileName,
                DefaultExtension = "pdf",
                FileTypeChoices = new[] { new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } } }
            });
            if (file is null) return;

            var bytes = PdfExportService.Render(_vm.CreateWeekExport());
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(bytes);

            LogService.Info("PDF exportiert: {0}", file.Name);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler beim PDF-Export", ex);
        }
    }

    private async void OnDayNoteDialogRequested(DateOnly date, string note)
    {
        try
        {
            if (App.DialogService is null) { LogService.Warn("Kein Dialog-Backend verfügbar."); return; }

            var result = await App.DialogService.ShowDayNoteAsync(new DayNoteViewModel(date, note));

            if (result is not null && _vm is not null)
                await _vm.ApplyDayNoteAsync(date, result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Tages-Hinweis-Dialog", ex);
        }
    }

    /// <summary>Klick auf einen Eintrag in einer Tabellenzelle → Bearbeiten. Stoppt das Bubbling (kein Neu-Anlegen).</summary>
    private void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;
        if (sender is not Control { DataContext: CalendarEntry entry }) return;

        var cell = _vm.Rows.SelectMany(r => r.Cells).FirstOrDefault(c => c.Entries.Contains(entry));
        if (cell != null)
        {
            e.Handled = true;
            _vm.ActivateEntry(cell.Date, entry);
        }
    }

    /// <summary>Klick in eine (leere) Zelle → Eintrag für diese Person an diesem Tag anlegen.</summary>
    private void OnCellTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;
        if (sender is not Control { DataContext: PersonDayCellViewModel cell }) return;
        if (cell.CanAdd) _vm.AddForCell(cell.Person, cell.Date);
    }

    private async void OnEntryDialogRequested(DateOnly date, CalendarEntry? existing, IReadOnlyList<User> users,
        bool canPickUser, IReadOnlyList<EntryType> allowedTypes, IReadOnlyList<ActivityType> activityTypes)
    {
        try
        {
            if (App.DialogService is null) { LogService.Warn("Kein Dialog-Backend verfügbar."); return; }

            var vm = existing is null
                ? new EntryEditorViewModel(date, users, canPickUser, allowedTypes, activityTypes)
                : new EntryEditorViewModel(date, users, existing, canPickUser, allowedTypes, activityTypes);

            var result = await App.DialogService.ShowEntryEditorAsync(vm);

            if (result is not null && _vm is not null)
                await _vm.ApplyEntryResultAsync(date, result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Eintrag-Dialog", ex);
        }
    }

    private async void OnSwapDialogRequested(ShiftSwapViewModel vm)
    {
        try
        {
            if (App.DialogService is null) { LogService.Warn("Kein Dialog-Backend verfügbar."); return; }

            var result = await App.DialogService.ShowShiftSwapAsync(vm);

            if (result is not null && _vm is not null)
                await _vm.ApplySwapResultAsync(result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Tausch-Dialog", ex);
        }
    }

    private async void OnReplanDialogRequested(ReplanViewModel vm)
    {
        try
        {
            if (App.DialogService is null) { LogService.Warn("Kein Dialog-Backend verfügbar."); return; }

            var result = await App.DialogService.ShowReplanAsync(vm);

            if (result is not null && _vm is not null)
                await _vm.ApplyReplanResultAsync(result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Umplanungs-Dialog", ex);
        }
    }

    // ───────── Drag&Drop: Schicht-Chip → andere Zelle ─────────

    /// <summary>Startet den Drag eines Eintrag-Chips. Avalonia kümmert sich selbst um den Bewegungs-
    /// Threshold; ein reiner Klick (ohne Bewegung) feuert weiterhin Tapped → Eintrag bearbeiten.</summary>
    private async void OnEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm?.IsAdmin != true) return;
        if (sender is not Control ctrl || ctrl.DataContext is not CalendarEntry entry) return;
        if (!EntryMoveCopy.CanDrag(entry)) return;

        var cell = FindAncestorContext<PersonDayCellViewModel>(ctrl);
        if (cell is null) return;

        var item = new DataTransferItem();
        item.Set(EntryIdFormat, entry.Id);
        item.Set(SourceDateFormat, cell.Date.ToString("yyyy-MM-dd"));
        var transfer = new DataTransfer();
        transfer.Add(item);

        // Visuelles Feedback: Source-Chip leicht ausblenden, solange er „unterwegs" ist.
        var originalOpacity = ctrl.Opacity;
        ctrl.Opacity = 0.4;
        try
        {
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            LogService.Error("Drag&Drop fehlgeschlagen", ex);
        }
        finally
        {
            ctrl.Opacity = originalOpacity;
        }
    }

    /// <summary>Wird beim ersten Layout der Tageszelle aufgerufen — registriert sie als Drop-Target.</summary>
    private void OnCellLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c) return;
        c.AddHandler(DragDrop.DragOverEvent, OnCellDragOver);
        c.AddHandler(DragDrop.DragLeaveEvent, OnCellDragLeave);
        c.AddHandler(DragDrop.DropEvent, OnCellDrop);
    }

    private void OnCellDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(EntryIdFormat))
        {
            e.DragEffects &= (DragDropEffects.Move | DragDropEffects.Copy);
            if (sender is Border b) b.Background = DropTargetBrush;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnCellDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border b) b.Background = Brushes.Transparent;
    }

    private async void OnCellDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not PersonDayCellViewModel cell) return;
        var entryId = e.DataTransfer.TryGetValue(EntryIdFormat);
        var srcStr = e.DataTransfer.TryGetValue(SourceDateFormat);
        if (string.IsNullOrEmpty(entryId) || string.IsNullOrEmpty(srcStr)) return;
        if (!DateOnly.TryParse(srcStr, out var srcDate)) return;

        e.Handled = true;
        if (c is Border b) b.Background = Brushes.Transparent;
        if (_vm is null) return;

        try
        {
            await _vm.HandleEntryDropAsync(entryId, srcDate, cell);
        }
        catch (Exception ex)
        {
            LogService.Error("Drop-Verarbeitung fehlgeschlagen", ex);
        }
    }

    private static T? FindAncestorContext<T>(Control? start) where T : class
    {
        Visual? v = start;
        while (v is not null)
        {
            if (v is Control x && x.DataContext is T t) return t;
            v = v.GetVisualParent();
        }
        return null;
    }
}
