using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
        PreviewRows.PreviewMouseMove += List_MouseMove;
        PreviewRows.MouseDoubleClick += Preview_DoubleClick;

        AvailableList.PreviewMouseLeftButtonDown += List_MouseDown;
        AvailableList.PreviewMouseMove += List_MouseMove;
        AvailableList.MouseDoubleClick += Available_DoubleClick;

        // Dropping onto the frame rather than a row removes the item: the
        // gesture for taking a button off the toolbar is dragging it away from
        // the toolbar, which has to land somewhere.
        AvailableList.AllowDrop = true;
        AvailableList.Drop += Available_Drop;
        AvailableList.DragOver += (_, e) => e.Effects = DragDropEffects.Move;
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
    /// Actions are unique — a second Merge button would be a second way to do
    /// the same thing in the same row — so one that is in use drops out of the
    /// list. The fillers never do: a layout may want any number of spacers,
    /// expanders and rows.
    /// </remarks>
    private void RefreshAvailable()
    {
        var placed = _rows.SelectMany(r => r).Select(i => i.Key).ToHashSet();
        var filter = SearchBox.Text.Trim();

        _available.Clear();

        foreach (var item in _catalogue)
        {
            if (item.Kind == ToolbarItemKind.Button && placed.Contains(item.Key)) continue;

            if (filter.Length > 0 &&
                !item.Label.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !item.Group.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            _available.Add(item.Clone());
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
        _pressPoint = e.GetPosition(this);
        _pressed = ItemUnder(e.OriginalSource as DependencyObject);
    }

    private void List_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressed is null || _dragging is not null) return;

        var at = e.GetPosition(this);
        if (Math.Abs(at.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(at.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragging = _pressed;
        _draggingFrom = _rows.FirstOrDefault(r => r.Contains(_pressed));
        _dragging.IsDragging = true;

        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, _dragging, DragDropEffects.Move);
        }
        finally
        {
            _dragging.IsDragging = false;
            _dragging = null;
            _draggingFrom = null;
            _pressed = null;
            RefreshAvailable();
        }
    }

    /// <summary>Lights the row the drop would land in.</summary>
    /// <remarks>
    /// Only worth showing once there is more than one row — with a single row
    /// there is nothing to choose between, and a border that is always on is a
    /// border nobody reads.
    /// </remarks>
    private void Row_DragEnter(object sender, DragEventArgs e)
    {
        if (_rows.Count > 1 && sender is Border row)
            row.BorderBrush = Services.ThemeService.Brush(nameof(Services.ThemePalette.Accent));
    }

    private void Row_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border row) row.BorderBrush = Brushes.Transparent;
    }

    private void Row_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void Row_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border border) border.BorderBrush = Brushes.Transparent;
        if (_dragging is null || sender is not Border { DataContext: ObservableCollection<ToolbarItem> target })
            return;

        var item = _dragging;

        // A row break dropped onto a row splits it there rather than becoming a
        // chip in it: it is the one entry with no width of its own, and leaving
        // it in the row would be a button-shaped thing that draws nothing.
        if (item.Kind == ToolbarItemKind.RowBreak)
        {
            SplitRow(target, DropIndex(sender, e, target));
            e.Handled = true;
            return;
        }

        _draggingFrom?.Remove(item);

        var index = Math.Clamp(DropIndex(sender, e, target), 0, target.Count);
        target.Insert(index, item);

        DropEmptyRows();
        e.Handled = true;
    }

    /// <summary>Where in the row the pointer is, counted in items.</summary>
    private static int DropIndex(object sender, DragEventArgs e, ObservableCollection<ToolbarItem> row)
    {
        if (sender is not Border border) return row.Count;

        var at = e.GetPosition(border).X;
        var panel = FindPanel(border);
        if (panel is null) return row.Count;

        for (var i = 0; i < panel.Children.Count && i < row.Count; i++)
        {
            var child = (UIElement)panel.Children[i];
            var left = child.TranslatePoint(new Point(0, 0), border).X;

            // Past the middle of a chip means after it, which is what makes
            // dropping onto the right-hand half of a button put the new one
            // behind it rather than in front.
            if (at < left + child.RenderSize.Width / 2) return i;
        }

        return row.Count;
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

    /// <summary>Dropping onto the Available side takes the item off the toolbar.</summary>
    private void Available_Drop(object sender, DragEventArgs e)
    {
        if (_dragging is null || _draggingFrom is null) return;

        _draggingFrom.Remove(_dragging);
        DropEmptyRows();
        e.Handled = true;
    }

    // ------------------------------------------------------------------
    // Double-click, for everyone who would rather not drag
    // ------------------------------------------------------------------

    private void Available_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder(e.OriginalSource as DependencyObject) is not { } item) return;

        if (item.Kind == ToolbarItemKind.RowBreak) _rows.Add(new ObservableCollection<ToolbarItem>());
        else _rows[^1].Add(item.Clone());

        RefreshAvailable();
    }

    private void Preview_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder(e.OriginalSource as DependencyObject) is not { } item) return;

        _rows.FirstOrDefault(r => r.Contains(item))?.Remove(item);
        DropEmptyRows();
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
        var flat = new List<ToolbarItem>();

        foreach (var row in _rows.Where(r => r.Count > 0))
        {
            if (flat.Count > 0)
                flat.Add(new ToolbarItem { Key = "row-break", Kind = ToolbarItemKind.RowBreak, Label = "New row" });

            flat.AddRange(row);
        }

        Layout = flat;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
