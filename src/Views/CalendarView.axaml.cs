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

    // Drop-Ziel-Highlight (transparentes Blau) — wird während des Drags auf der Ziel-Zelle
    // eingesetzt. Bewusst kein Avalonia DragDrop-System, weil DoDragDropAsync im Browser/WASM
    // nur eingeschränkt funktioniert; wir tracken die Geste rein per Pointer-Events.
    private static readonly IBrush DropTargetBrush =
        new SolidColorBrush(Color.FromArgb(0x55, 0x2E, 0x86, 0xC1));

    // Drag-Pending: erst nach echter Bewegung > 5px startet der Drag — sonst verschluckt
    // die Pointer-Verarbeitung das Tapped-Event und der Editor öffnet sich nicht mehr.
    private CalendarEntry? _pendingDragEntry;
    private PersonDayCellViewModel? _pendingDragCell;
    private Control? _pendingDragCtrl;
    private Point? _pendingDragStart;
    private bool _dragStarted;
    private double _pendingDragOriginalOpacity = 1.0;
    private Border? _highlightedDropCell;
    private IBrush? _highlightedDropCellPrev;

    // Analog für den Reorder-Drag auf Zeilenebene (Admin schiebt eine Personenzeile).
    private PersonRowViewModel? _pendingRowDragRow;
    private Control? _pendingRowDragCtrl;
    private Point? _pendingRowDragStart;
    private bool _rowDragStarted;
    private double _pendingRowDragOriginalOpacity = 1.0;

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
        if (App.DialogService is null) { LogService.Warn("Kein Dialog-Backend verfügbar."); return; }
        try
        {
            var result = await App.DialogService.ShowMailAsync(vm);
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

    private async void OnDayNoteDialogRequested(DateOnly date, string note, string? noteUserId)
    {
        try
        {
            if (App.DialogService is null) { LogService.Warn("Kein Dialog-Backend verfügbar."); return; }
            if (_vm is null) return;

            var result = await App.DialogService.ShowDayNoteAsync(
                new DayNoteViewModel(date, note, noteUserId, _vm.AllUsers));

            if (result is not null)
                await _vm.ApplyDayNoteAsync(date, result.Note, result.NoteUserId);
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
        // Wenn der Tap aus einem Button (z.B. dem Add-More-„+") kommt, ist der Click-Handler
        // dort schon zuständig — sonst würde AddForCell zweimal feuern und der Dialog doppelt
        // erscheinen. Click-`Handled` stoppt nur das Click-Event, nicht das parallele Tapped.
        if (e.Source is Visual v && v.GetSelfAndVisualAncestors().OfType<Button>().Any()) return;
        if (cell.CanAdd) _vm.AddForCell(cell.Person, cell.Date);
    }

    /// <summary>„+"-Button in einer voll belegten Zelle → zusätzlichen Eintrag anlegen.</summary>
    private void OnAddMoreClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null) return;
        if (sender is not Control { DataContext: PersonDayCellViewModel cell }) return;
        if (cell.CanAdd) _vm.AddForCell(cell.Person, cell.Date);
        e.Handled = true;
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

    // ───────── Drag&Drop: Schicht-Chip → andere Zelle (rein Pointer-basiert, WASM-tauglich) ─────────

    /// <summary>Pointer-Pressed merkt nur die Start-Position und captured den Pointer; der echte Drag startet
    /// erst, wenn PointerMoved eine Bewegung > 5px sieht — sonst würde ein einfacher Klick als Drag missdeutet.</summary>
    private void OnEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ClearPendingDrag();
        if (_vm?.IsAdmin != true) return;
        if (sender is not Control ctrl || ctrl.DataContext is not CalendarEntry entry) return;
        if (!EntryMoveCopy.CanDrag(entry)) return;

        var cell = FindAncestorContext<PersonDayCellViewModel>(ctrl);
        if (cell is null) return;

        _pendingDragEntry = entry;
        _pendingDragCell = cell;
        _pendingDragCtrl = ctrl;
        _pendingDragStart = e.GetPosition(this);
        // Kein manuelles Capture hier — würde den Tapped-Event auf leeren Zellen unterbinden
        // (Klick zum Anlegen einer neuen Schicht). Capture wird erst gesetzt, wenn wir ab
        // 5 px Bewegung wirklich in den Drag-Modus wechseln.
    }

    private void OnEntryPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingDragCtrl is null || _pendingDragStart is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { CancelPendingDrag(); return; }

        var p = e.GetPosition(this);
        var dx = p.X - _pendingDragStart.Value.X;
        var dy = p.Y - _pendingDragStart.Value.Y;
        if (!_dragStarted)
        {
            if (dx * dx + dy * dy < 25) return;   // unter 5px = Klick, kein Drag
            _dragStarted = true;
            _pendingDragOriginalOpacity = _pendingDragCtrl.Opacity;
            _pendingDragCtrl.Opacity = 0.4;
            // Ab jetzt ist es ein echter Drag → Pointer capturen, damit wir auch außerhalb
            // des Chips weitere Moves und das Release bekommen.
            e.Pointer.Capture(_pendingDragCtrl);
        }

        UpdateDropTargetHighlight(HitTestDropCell(p));
    }

    private async void OnEntryPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            e.Pointer.Capture(null);
            if (!_dragStarted) return;   // reiner Klick → Tapped übernimmt

            var target = HitTestDropCell(e.GetPosition(this));
            UpdateDropTargetHighlight(null);
            if (target is null || _vm is null) return;

            var cell = FindAncestorContext<PersonDayCellViewModel>(target);
            if (cell is null || _pendingDragEntry is null || _pendingDragCell is null) return;

            try
            {
                await _vm.HandleEntryDropAsync(_pendingDragEntry.Id, _pendingDragCell.Date, cell);
            }
            catch (Exception ex)
            {
                LogService.Error("Drop-Verarbeitung fehlgeschlagen", ex);
            }
        }
        finally
        {
            ClearPendingDrag();
        }
    }

    private void CancelPendingDrag()
    {
        UpdateDropTargetHighlight(null);
        ClearPendingDrag();
    }

    private void ClearPendingDrag()
    {
        if (_pendingDragCtrl is not null && _dragStarted)
            _pendingDragCtrl.Opacity = _pendingDragOriginalOpacity;
        _pendingDragEntry = null;
        _pendingDragCell = null;
        _pendingDragCtrl = null;
        _pendingDragStart = null;
        _dragStarted = false;
        _pendingDragOriginalOpacity = 1.0;
    }

    /// <summary>Sucht per HitTest die Border-Zelle unter dem angegebenen Punkt und liefert sie zurück,
    /// falls sie an einen <see cref="PersonDayCellViewModel"/> gebunden ist.</summary>
    private Border? HitTestDropCell(Point p)
    {
        var hit = this.InputHitTest(p) as Visual;
        while (hit is not null)
        {
            if (hit is Border b && b.DataContext is PersonDayCellViewModel) return b;
            hit = hit.GetVisualParent();
        }
        return null;
    }

    /// <summary>Setzt/entfernt die visuelle Hervorhebung der aktuellen Ziel-Zelle. Merkt sich den
    /// vorherigen Hintergrund, damit auch nicht-transparente Wochentags-Highlights sauber
    /// wiederhergestellt werden.</summary>
    private void UpdateDropTargetHighlight(Border? newTarget)
    {
        if (ReferenceEquals(_highlightedDropCell, newTarget)) return;
        if (_highlightedDropCell is not null)
            _highlightedDropCell.Background = _highlightedDropCellPrev;
        _highlightedDropCell = newTarget;
        if (newTarget is not null)
        {
            _highlightedDropCellPrev = newTarget.Background;
            newTarget.Background = DropTargetBrush;
        }
        else
        {
            _highlightedDropCellPrev = null;
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

    // ───────── Pointer-basiertes Reorder: Personenzeile → neue Position (Admin) ─────────

    /// <summary>Wird auf jeder Zeile beim ersten Layout aufgerufen — registriert Pointer-Handler,
    /// die den Klick auf den inneren Impersonate-Button nicht schlucken (handledEventsToo).</summary>
    private void OnRowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c) return;
        c.AddHandler(PointerPressedEvent, OnRowPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        c.AddHandler(PointerMovedEvent, OnRowPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        c.AddHandler(PointerReleasedEvent, OnRowPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ClearPendingRowDrag();
        if (_vm?.EffectiveIsAdmin != true) return;
        if (sender is not Control ctrl || ctrl.DataContext is not PersonRowViewModel row) return;
        if (!row.CanReorder) return;

        _pendingRowDragRow = row;
        _pendingRowDragCtrl = ctrl;
        _pendingRowDragStart = e.GetPosition(this);
        // Kein manuelles Capture hier — würde den Tapped-Event auf leeren Zellen unterbinden
        // (Klick zum Anlegen einer neuen Schicht). Capture kommt erst beim echten Drag-Start.
    }

    private void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingRowDragCtrl is null || _pendingRowDragStart is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { ClearPendingRowDrag(); return; }
        if (_rowDragStarted) return;

        var p = e.GetPosition(this);
        var dx = p.X - _pendingRowDragStart.Value.X;
        var dy = p.Y - _pendingRowDragStart.Value.Y;
        if (dx * dx + dy * dy < 25) return;

        _rowDragStarted = true;
        _pendingRowDragOriginalOpacity = _pendingRowDragCtrl.Opacity;
        _pendingRowDragCtrl.Opacity = 0.4;
        e.Pointer.Capture(_pendingRowDragCtrl);
    }

    /// <summary>
    /// Ohne echten Drag wird hier NICHTS angefasst — insbesondere nicht das Pointer-Capture.
    /// Dieser Handler hängt an der Zeile und läuft wegen <see cref="RoutingStrategies.Tunnel"/>
    /// VOR den Controls darunter. Ein <c>Capture(null)</c> an dieser Stelle schickt jedem
    /// Button in der Zeile ein <c>PointerCaptureLost</c>; Avalonias Button setzt darauf intern
    /// <c>IsPressed=false</c> und lässt beim Release seinen Click ausfallen. Genau daran ist
    /// der „+"-Knopf in der Tageszelle gestorben: Tapped greift bei ihm nicht (OnCellTapped
    /// blendet Buttons bewusst aus), Click kam nie an — der Dialog blieb zu.
    /// </summary>
    private async void OnRowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_rowDragStarted) return;

        // Zustand einsammeln und sofort zurücksetzen: der Handler ist für Tunnel UND Bubble
        // registriert, läuft pro Release also zweimal. Das await unten gibt zwischendurch die
        // Kontrolle ab — ohne das Zurücksetzen VOR dem await würde der zweite Durchlauf
        // denselben Reorder ein weiteres Mal auslösen.
        var draggedRow = _pendingRowDragRow;
        ClearPendingRowDrag();
        e.Pointer.Capture(null);

        var targetRow = FindRowViewModelAt(e.GetPosition(this));
        if (targetRow is null || _vm is null || draggedRow is null) return;

        try
        {
            await _vm.ReorderPersonAsync(draggedRow.UserId, targetRow.UserId);
        }
        catch (Exception ex)
        {
            LogService.Error("Personen-Reihenfolge speichern fehlgeschlagen", ex);
        }
    }

    private void ClearPendingRowDrag()
    {
        if (_pendingRowDragCtrl is not null && _rowDragStarted)
            _pendingRowDragCtrl.Opacity = _pendingRowDragOriginalOpacity;
        _pendingRowDragRow = null;
        _pendingRowDragCtrl = null;
        _pendingRowDragStart = null;
        _rowDragStarted = false;
        _pendingRowDragOriginalOpacity = 1.0;
    }

    private PersonRowViewModel? FindRowViewModelAt(Point p)
    {
        var hit = this.InputHitTest(p) as Visual;
        while (hit is not null)
        {
            if (hit is Control c && c.DataContext is PersonRowViewModel row) return row;
            hit = hit.GetVisualParent();
        }
        return null;
    }

}
