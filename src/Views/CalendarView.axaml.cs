using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class CalendarView : UserControl
{
    private CalendarViewModel? _vm;

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
}
