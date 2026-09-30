using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Builds a toolbar layout: which buttons are on it, in what order, on how many
/// rows.
/// </summary>
/// <remarks>
/// Edits copies and hands the result back through <see cref="Layout"/>, so
/// Cancel really cancels — nothing here touches the running toolbar.
///
/// The preview is the toolbar, not a picture of one: same item template, same
/// row panel, drawn at the width the window is allowed to shrink to. So a row
/// that fits here fits everywhere, and a row that is too long wraps in the
/// preview exactly as it will in the program.
/// </remarks>
public partial class CustomizeToolbarDialog : Window
{
    /// <summary>The layout to apply, or null when the dialog was cancelled.</summary>
    public List<ToolbarItem>? Layout { get; private set; }

    /// <summary>Everything that can be put on a toolbar.</summary>
    private readonly List<ToolbarItem> _catalogue;

    /// <summary>The rows being edited. Never empty — there is always one row.</summary>
    private readonly ObservableCollection<ObservableCollection<ToolbarItem>> _rows = new();

    /// <summary>What is left to choose from, filtered by the search box.</summary>
    private readonly ObservableCollection<ToolbarItem> _available = new();

    /// <summary>The item being dragged, and where it came from.</summary>
    private ToolbarItem? _dragging;
    private ObservableCollection<ToolbarItem>? _draggingFrom;
    private Point _pressPoint;
    private ToolbarItem? _pressed;

    /// <summary>The element pressed, kept for the picture the ghost carries.</summary>
    private FrameworkElement? _pressedElement;

    /// <summary>The copy of it that follows the pointer while it is being carried.</summary>
    private DragGhostAdorner? _ghost;

    /// <summary>Where the pointer was on the last move, for a drag that ends by losing capture.</summary>
    private Point _lastDragPoint;

    public CustomizeToolbarDialog(List<ToolbarItem> catalogue,
                                  IEnumerable<ToolbarItem> current,
                                  double minimumWidth)
    {
        InitializeComponent();

        _catalogue = catalogue;

        // The preview is bounded by the width the toolbar can always count on.
        // Padding and the border are not toolbar, so they come off it.
        PreviewFrame.Width = Math.Max(320, minimumWidth) + 14;
        WidthNote.Text = $"the window's narrowest width — {Math.Max(320, minimumWidth):0} px";

        LoadRows(current);
        RefreshAvailable();

        PreviewRows.ItemsSource = _rows;
        AvailableList.ItemsSource = BuildGroupedView();

        PreviewRows.PreviewMouseLeftButtonDown += List_MouseDown;
        PreviewRows.MouseDoubleClick += Preview_DoubleClick;

        AvailableList.PreviewMouseLeftButtonDown += List_MouseDown;
        AvailableList.MouseDoubleClick += Available_DoubleClick;

    }

    // ------------------------------------------------------------------
    // Building the two sides
    // ------------------------------------------------------------------

    private void LoadRows(IEnumerable<ToolbarItem> layout)
    {
        _rows.Clear();
        var row = new ObservableCollection<ToolbarItem>();

        foreach (var item in layout)
        {
            if (item.Kind == ToolbarItemKind.RowBreak)
            {
                _rows.Add(row);
                row = new ObservableCollection<ToolbarItem>();
                continue;
            }

            row.Add(item.Clone());
        }

        _rows.Add(row);
    }

    /// <summary>
    /// Refills the Available list: every action not already placed, plus the
    /// three fillers.
    /// </summary>
    /// <remarks>
    /// Everything stays listed, including what is already on the toolbar —
    /// dimmed, and refused if dropped. Dropping placed actions out of the list
    /// hid them completely, so somebody looking for Save frame or Convert found
    /// no trace of either and reasonably concluded they were missing. A greyed
    /// entry answers that: it is here, and it is already on your toolbar.
    ///
    /// Actions are still unique. A second Merge button would be a second way to
    /// do one thing in one row. The fillers are not: a layout may want any
    /// number of spacers, expanders and rows.
    /// </remarks>
    private void RefreshAvailable()
    {
        var placed = _rows.SelectMany(r => r).Select(i => i.Key).ToHashSet();
        var filter = SearchBox.Text.Trim();

        _available.Clear();

        foreach (var item in _catalogue)
        {
            if (filter.Length > 0 &&
                !item.Label.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !item.Group.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var copy = item.Clone();
            copy.IsPlaced = item.Kind == ToolbarItemKind.Button && placed.Contains(item.Key);
            _available.Add(copy);
        }
    }

    /// <summary>Available, grouped by the menu each action lives in.</summary>
    private ICollectionView BuildGroupedView()
    {
        var view = CollectionViewSource.GetDefaultView(_available);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolbarItem.Group)));
        return view;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => RefreshAvailable();

    // ------------------------------------------------------------------
    // Dragging
    // ------------------------------------------------------------------

    private void List_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(RootGrid);
        _pressedElement = ChipUnder(e.OriginalSource as DependencyObject);
        _pressed = _pressedElement?.DataContext as ToolbarItem;
    }

    /// <summary>
    /// Starts carrying the item once the pointer has moved far enough, and
    /// keeps the ghost under it.
    /// </summary>
    /// <remarks>
    /// On the window rather than on the two lists, which is not a detail. The
    /// drag captures the mouse, and a captured mouse sends its moves to the
    /// capturing element and nowhere else — so handlers on the lists stop
    /// hearing anything the moment the drag begins. They did, and the
    /// consequences were a ghost frozen where it was picked up and a drop
    /// landing wherever the pointer had been a moment before the capture.
    ///
    /// Not <c>DragDrop.DoDragDrop</c>, which is for carrying data between
    /// applications and shows nothing but a cursor. What is being moved here is
    /// on screen, and the useful feedback is watching it move.
    /// </remarks>
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_pressed is null || _pressedElement is null) return;

        var at = e.GetPosition(RootGrid);

        if (_dragging is null)
        {
            if (Math.Abs(at.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(at.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragging = _pressed;
            _draggingFrom = _rows.FirstOrDefault(r => r.Contains(_pressed));
            _dragging.IsDragging = true;

            _ghost = new DragGhostAdorner(RootGrid, _pressedElement);
            AdornerLayer.GetAdornerLayer(RootGrid)?.Add(_ghost);

            CaptureMouse();
        }

        _lastDragPoint = at;
        _ghost?.MoveTo(at);
        HighlightRowUnder(at);
    }

    /// <summary>Puts the carried item down wherever the pointer is.</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (_dragging is null)
        {
            _pressed = null;
            _pressedElement = null;
            return;
        }

        CompleteDrag(e.GetPosition(RootGrid));
        e.Handled = true;
    }

    /// <summary>
    /// Finishes a drag that ended by losing the mouse rather than by a release
    /// this window saw.
    /// </summary>
    /// <remarks>
    /// Releasing the button can take the capture away before the button-up is
    /// delivered here, and the drop was being thrown away when it did: the
    /// teardown ran first, the up found nothing in hand, and the carried button
    /// went back where it came from. Whichever arrives first now completes the
    /// drag; the other finds nothing to do.
    ///
    /// The position comes from the last move rather than from this event, which
    /// carries none — it is where the pointer was when the drag ended, which is
    /// the same thing.
    /// </remarks>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragging is not null) CompleteDrag(_lastDragPoint);
    }

    private void CompleteDrag(Point at)
    {
        var item = _dragging;
        var from = _draggingFrom;
        if (item is null) return;

        // The ghost comes off before the hit test, not after. It is marked
        // not-hit-testable, but its layer sits over everything, and asking what
        // is under the pointer while it is still up gave an answer from the
        // adorner layer rather than from the toolbar — so every drop read as
        // "nowhere in particular" and fell to the end of the row.
        EndDrag();

        var (row, index, overAvailable) = ResolveDrop(at);

        if (row is not null) Place(item, from, row, index);
        else if (overAvailable && from is not null) Remove(item);

        RefreshAvailable();
    }

    private void EndDrag()
    {
        if (_ghost is not null)
        {
            AdornerLayer.GetAdornerLayer(RootGrid)?.Remove(_ghost);
            _ghost = null;
        }

        if (_dragging is not null) _dragging.IsDragging = false;

        _dragging = null;
        _draggingFrom = null;
        _pressed = null;
        _pressedElement = null;

        ClearRowHighlights();
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>The chip element under a hit-tested point, or null.</summary>
    /// <remarks>
    /// Used to pick an item <em>up</em>, where the pointer is over a real chip
    /// and nothing is covering it. Putting one down asks
    /// <see cref="ResolveDrop"/> instead, for the reason given there.
    /// </remarks>
    private static FrameworkElement? ChipUnder(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: ToolbarItem } chip) return chip;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    /// <summary>
    /// Lights the row the drop would land in, and clears the others.
    /// </summary>
    /// <remarks>
    /// Only once there is more than one row: with a single row there is nothing
    /// to choose between, and a border that is always on is a border nobody
    /// reads.
    /// </remarks>
    private void HighlightRowUnder(Point at)
    {
        ClearRowHighlights();
        if (_rows.Count < 2) return;

        foreach (var frame in RowFrames())
            if (Bounds(frame).Contains(at))
            {
                frame.BorderBrush = Services.ThemeService.Brush(nameof(Services.ThemePalette.Accent));
                return;
            }
    }

    private void ClearRowHighlights()
    {
        foreach (var frame in RowFrames()) frame.BorderBrush = Brushes.Transparent;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    /// <summary>
    /// Where a point falls: which row, which position in it, or the Available
    /// list.
    /// </summary>
    /// <remarks>
    /// Measured rather than hit-tested. A hit test answers "what is on top
    /// here", and while a drag is running what is on top is the adorner layer
    /// carrying the ghost — so the answers came back from the wrong tree and
    /// drops landed at the end of the row, or read as the Available list and
    /// deleted the button. Rectangles do not have that problem: a row is where
    /// it is whatever is drawn over it.
    ///
    /// Bounds come through TransformToAncestor, so a row inside a border inside
    /// a panel is still measured in this window's own coordinates — the same
    /// ones the pointer is reported in.
    /// </remarks>
    private (ObservableCollection<ToolbarItem>? Row, int Index, bool OverAvailable) ResolveDrop(Point at)
    {
        foreach (var frame in RowFrames())
        {
            if (frame.DataContext is not ObservableCollection<ToolbarItem> items) continue;
            if (!Bounds(frame).Contains(at)) continue;

            // The first chip whose middle is past the pointer is the one it
            // goes in front of. None of them means the empty space at the end.
            for (var i = 0; i < items.Count; i++)
            {
                if (ChipFor(frame, items[i]) is not { } chip) continue;

                var box = Bounds(chip);
                if (at.X < box.Left + box.Width / 2) return (items, i, false);
            }

            return (items, items.Count, false);
        }

        return Bounds(AvailableList).Contains(at) ? (null, 0, true) : (null, 0, false);
    }

    /// <summary>
    /// The bounds of an element in the content root's coordinates.
    /// </summary>
    /// <remarks>
    /// RootGrid, not the window. A Window reports pointer positions in its
    /// client area but measures its visual tree from below the chrome, so the
    /// two are a title bar apart — and comparing one against the other put
    /// every drop about thirty pixels below where it looked. One element for
    /// both, and the question stops depending on which.
    /// </remarks>
    private Rect Bounds(FrameworkElement element) =>
        element.TransformToAncestor(RootGrid)
               .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    /// <summary>The frame drawn around each row of the preview.</summary>
    private IEnumerable<Border> RowFrames() =>
        Descendants(PreviewRows).OfType<Border>()
            .Where(b => b.DataContext is ObservableCollection<ToolbarItem> && b.ActualWidth > 0);

    /// <summary>The chip drawn for one item inside a row.</summary>
    private static FrameworkElement? ChipFor(DependencyObject row, ToolbarItem item) =>
        Descendants(row).OfType<FrameworkElement>()
            .FirstOrDefault(e => e is Grid && ReferenceEquals(e.DataContext, item) && e.ActualWidth > 0);


    /// <summary>
    /// Puts an item into a row at a position, taking it out of wherever it was.
    /// </summary>
    /// <remarks>
    /// The one place the layout is edited, so a drop, a double-click and a test
    /// all go through the same rules rather than three approximations of them.
    /// </remarks>
    internal void Place(ToolbarItem item, ObservableCollection<ToolbarItem>? from,
                        ObservableCollection<ToolbarItem> target, int index)
    {
        // Already on the toolbar and being dragged in from the list again —
        // the greyed copy. Nothing to do: the real one is where the user put it.
        if (from is null && item.Kind == ToolbarItemKind.Button &&
            _rows.Any(r => r.Any(i => i.Key == item.Key)))
            return;

        // A row break splits the row it lands in rather than sitting in it: it
        // is the one entry with no width of its own, and leaving it in the row
        // would be a button-shaped thing that draws nothing.
        if (item.Kind == ToolbarItemKind.RowBreak)
        {
            SplitRow(target, index);
            return;
        }

        // Where it is being taken from, before it is taken, because the index
        // asked for was read off the row as it looks now — with this item still
        // in it. Removing it first shifts everything behind it down one, so a
        // move to the right would land one place too far without this.
        var wasAt = from?.IndexOf(item) ?? -1;

        from?.Remove(item);

        if (ReferenceEquals(from, target) && wasAt >= 0 && wasAt < index) index--;

        target.Insert(Math.Clamp(index, 0, target.Count), item);
        DropEmptyRows();
    }

    /// <summary>Takes an item off the toolbar.</summary>
    internal void Remove(ToolbarItem item)
    {
        _rows.FirstOrDefault(r => r.Contains(item))?.Remove(item);
        DropEmptyRows();
    }

    /// <summary>The rows as they stand, for tests.</summary>
    internal IReadOnlyList<ObservableCollection<ToolbarItem>> Rows => _rows;

    /// <summary>The layout these rows would be saved as.</summary>
    internal List<ToolbarItem> Flatten()
    {
        var flat = new List<ToolbarItem>();

        foreach (var row in _rows.Where(r => r.Count > 0))
        {
            if (flat.Count > 0)
                flat.Add(new ToolbarItem { Key = "row-break", Kind = ToolbarItemKind.RowBreak, Label = "New row" });

            flat.AddRange(row);
        }

        return flat;
    }

    private static Panel? FindPanel(DependencyObject root)
    {
        if (root is Panel panel && root is not Grid) return panel;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindPanel(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;

        return null;
    }

    private void SplitRow(ObservableCollection<ToolbarItem> row, int at)
    {
        var index = _rows.IndexOf(row);
        if (index < 0) return;

        var tail = row.Skip(at).ToList();
        foreach (var item in tail) row.Remove(item);

        _rows.Insert(index + 1, new ObservableCollection<ToolbarItem>(tail));
    }

    /// <summary>
    /// Takes away rows that have been emptied, keeping at least one.
    /// </summary>
    private void DropEmptyRows()
    {
        for (var i = _rows.Count - 1; i >= 0 && _rows.Count > 1; i--)
            if (_rows[i].Count == 0)
                _rows.RemoveAt(i);
    }

    // ------------------------------------------------------------------
    // Double-click, for everyone who would rather not drag
    // ------------------------------------------------------------------

    private void Available_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder(e.OriginalSource as DependencyObject) is not { } item) return;

        if (item.Kind == ToolbarItemKind.RowBreak) _rows.Add(new ObservableCollection<ToolbarItem>());
        else Place(item.Clone(), null, _rows[^1], _rows[^1].Count);

        RefreshAvailable();
    }

    private void Preview_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder(e.OriginalSource as DependencyObject) is not { } item) return;

        Remove(item);
        RefreshAvailable();
    }

    private static ToolbarItem? ItemUnder(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: ToolbarItem item }) return item;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Leaving
    // ------------------------------------------------------------------

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Layout = new List<ToolbarItem>();       // Empty means "the shipped layout".
        DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Layout = Flatten();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
