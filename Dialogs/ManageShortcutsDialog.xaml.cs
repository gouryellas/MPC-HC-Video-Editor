using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Dialog for managing folder shortcuts: add, rename, remove, and
/// drag-reorder. Mutates the supplied <see cref="ObservableCollection{ShortcutEntry}"/>
/// in place; the caller is responsible for persisting any changes (the
/// <see cref="MainViewModel.ManageShortcutsCommand"/> calls
/// <c>_settings.ReorderShortcuts(...)</c> after the dialog closes).
/// </summary>
public partial class ManageShortcutsDialog : Window
{
    private readonly ObservableCollection<ShortcutEntry> _shortcuts;

    // Drag-and-drop state for reordering.
    private Point _dragStart;
    private object? _draggedItem;
    private bool _inDrag;

    private const string DragFormat = "MpcHcVideoEditor.ShortcutEntry";

    public ManageShortcutsDialog(ObservableCollection<ShortcutEntry> shortcuts)
    {
        InitializeComponent();
        _shortcuts = shortcuts;
        ShortcutList.ItemsSource = _shortcuts;
        UpdateEmptyHint();
        _shortcuts.CollectionChanged += (_, _) => UpdateEmptyHint();
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = _shortcuts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ----- Drag-reorder on the ListBox -----

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Remember where the press started; we only kick off a drag if the
        // mouse moves beyond the system drag threshold (avoids accidental
        // drags on a simple click-to-select).
        _dragStart = e.GetPosition(null);
        _draggedItem = ((ListBox)sender).InputHitTest(e.GetPosition((IInputElement)sender)) is DependencyObject d
            ? TryFindContainerItem(d)
            : null;
        _inDrag = _draggedItem != null;
    }

    private void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_inDrag || _draggedItem == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _inDrag = false;
            _draggedItem = null;
            return;
        }

        var pos = e.GetPosition(null);
        var dx = Math.Abs(pos.X - _dragStart.X);
        var dy = Math.Abs(pos.Y - _dragStart.Y);
        if (dx < SystemParameters.MinimumHorizontalDragDistance &&
            dy < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Walk from the hit-test result back to the ListBoxItem so we know
        // which entry the user grabbed.
        if (sender is not ListBox lb) return;
        var lbItem = _draggedItem as ListBoxItem
                     ?? (_draggedItem is DependencyObject d2 ? FindAncestor<ListBoxItem>(d2) : null);
        if (lbItem?.DataContext is not ShortcutEntry entry) return;

        _inDrag = false;
        try
        {
            DragDrop.DoDragDrop(lbItem, new DataObject(DragFormat, entry), DragDropEffects.Move);
        }
        catch { /* ignore aborted drags */ }
    }

    private void List_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void List_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragFormat)) return;
        var dragged = e.Data.GetData(DragFormat) as ShortcutEntry;
        if (dragged == null) return;

        if (sender is not ListBox lb) return;
        var target = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (target?.DataContext is not ShortcutEntry targetEntry) return;

        int from = _shortcuts.IndexOf(dragged);
        int to = _shortcuts.IndexOf(targetEntry);
        if (from < 0 || to < 0 || from == to) return;

        _shortcuts.Move(from, to);
        // Keep the dragged entry selected so the user sees where it landed.
        lb.SelectedIndex = to;
        e.Handled = true;
    }

    // ----- Buttons -----

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select folder to add as a shortcut" };
        if (dlg.ShowDialog() != true) return;

        var folder = dlg.FolderName;
        var trimmed = folder.TrimEnd('\\', '/');

        // De-dupe (case-insensitive on path).
        if (_shortcuts.Any(s => string.Equals(
            (s.Path ?? "").TrimEnd('\\', '/'), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("That folder is already in the list.", "Add shortcut",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name)) name = trimmed;

        var entry = new ShortcutEntry(folder, name);
        _shortcuts.Add(entry);
        ShortcutList.SelectedIndex = _shortcuts.Count - 1;
        ShortcutList.ScrollIntoView(entry);
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameSelected();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutList.SelectedItem is not ShortcutEntry entry) return;
        var result = MessageBox.Show(
            $"Remove the shortcut \"{entry.Name}\"?\n\nThe folder itself will not be touched.",
            "Remove shortcut", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        _shortcuts.Remove(entry);
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutList.SelectedItem is not ShortcutEntry entry) return;
        int i = _shortcuts.IndexOf(entry);
        if (i <= 0) return;
        _shortcuts.Move(i, i - 1);
        ShortcutList.SelectedIndex = i - 1;
    }

    private void Down_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutList.SelectedItem is not ShortcutEntry entry) return;
        int i = _shortcuts.IndexOf(entry);
        if (i < 0 || i >= _shortcuts.Count - 1) return;
        _shortcuts.Move(i, i + 1);
        ShortcutList.SelectedIndex = i + 1;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    // ----- Helpers -----

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only rename if the double-click landed on an item (not empty space).
        if (FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource) != null)
            RenameSelected();
    }

    private void RenameSelected()
    {
        if (ShortcutList.SelectedItem is not ShortcutEntry entry) return;

        var dlg = new InputDialog("Rename shortcut",
            $"New display name for:\n{entry.Path}",
            entry.Name)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var newName = dlg.Value.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("Name cannot be empty.", "Rename shortcut",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (newName != entry.Name) entry.Name = newName;
    }

    private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private static object? TryFindContainerItem(DependencyObject d)
    {
        // Walk up from the hit-test result until we find either a ListBoxItem
        // or the ListBox itself.
        while (d != null)
        {
            if (d is ListBoxItem lbi) return lbi;
            if (d is ListBox lb) return lb;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}
