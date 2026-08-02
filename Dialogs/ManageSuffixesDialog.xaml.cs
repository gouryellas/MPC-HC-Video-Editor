using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Dialog for managing filename suffixes: add, rename, remove, and
/// drag-reorder. Mutates the supplied <see cref="ObservableCollection{SuffixEntry}"/>
/// in place; the caller is responsible for persisting any changes (the
/// <see cref="MainViewModel.ManageSuffixesCommand"/> calls
/// <c>_settings.ReorderSuffixes(...)</c> after the dialog closes).
/// </summary>
public partial class ManageSuffixesDialog : Window
{
    private readonly ObservableCollection<SuffixEntry> _suffixes;

    // Drag-and-drop state for reordering.
    private Point _dragStart;
    private object? _draggedItem;
    private bool _inDrag;

    private const string DragFormat = "MpcHcVideoEditor.SuffixEntry";

    public ManageSuffixesDialog(ObservableCollection<SuffixEntry> suffixes)
    {
        InitializeComponent();
        _suffixes = suffixes;
        SuffixList.ItemsSource = _suffixes;
        UpdateEmptyHint();
        _suffixes.CollectionChanged += (_, _) => UpdateEmptyHint();
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = _suffixes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ----- Drag-reorder on the ListBox -----

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
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

        if (sender is not ListBox lb) return;
        var lbItem = _draggedItem as ListBoxItem
                     ?? (_draggedItem is DependencyObject d2 ? FindAncestor<ListBoxItem>(d2) : null);
        if (lbItem?.DataContext is not SuffixEntry entry) return;

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
        var dragged = e.Data.GetData(DragFormat) as SuffixEntry;
        if (dragged == null) return;

        if (sender is not ListBox lb) return;
        var target = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (target?.DataContext is not SuffixEntry targetEntry) return;

        int from = _suffixes.IndexOf(dragged);
        int to = _suffixes.IndexOf(targetEntry);
        if (from < 0 || to < 0 || from == to) return;

        _suffixes.Move(from, to);
        lb.SelectedIndex = to;
        e.Handled = true;
    }

    // ----- Buttons -----

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var text = PromptForSuffixText("Add suffix",
            "Enter suffix text (letters and numbers only, max 50 chars).\n\nIt will be wrapped in brackets in filenames, e.g. [done]",
            "");
        if (text == null) return;

        if (_suffixes.Any(s => string.Equals(s.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("That suffix already exists.", "Add suffix",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = new SuffixEntry(text);
        _suffixes.Add(entry);
        SuffixList.SelectedIndex = _suffixes.Count - 1;
        SuffixList.ScrollIntoView(entry);
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameSelected();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (SuffixList.SelectedItem is not SuffixEntry entry) return;
        var result = MessageBox.Show(
            $"Remove the suffix \"{entry.Display}\"?",
            "Remove suffix", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        _suffixes.Remove(entry);
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (SuffixList.SelectedItem is not SuffixEntry entry) return;
        int i = _suffixes.IndexOf(entry);
        if (i <= 0) return;
        _suffixes.Move(i, i - 1);
        SuffixList.SelectedIndex = i - 1;
    }

    private void Down_Click(object sender, RoutedEventArgs e)
    {
        if (SuffixList.SelectedItem is not SuffixEntry entry) return;
        int i = _suffixes.IndexOf(entry);
        if (i < 0 || i >= _suffixes.Count - 1) return;
        _suffixes.Move(i, i + 1);
        SuffixList.SelectedIndex = i + 1;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    // ----- Helpers -----

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource) != null)
            RenameSelected();
    }

    private void RenameSelected()
    {
        if (SuffixList.SelectedItem is not SuffixEntry entry) return;

        var text = PromptForSuffixText("Rename suffix",
            $"New text for:\n{entry.Display}\n\nLetters and numbers only, max 50 chars.",
            entry.Text);
        if (text == null || text == entry.Text) return;

        // Check for duplicates (excluding the entry being renamed).
        if (_suffixes.Any(s => s != entry &&
            string.Equals(s.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("A suffix with that text already exists.", "Rename suffix",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        entry.Text = text;
    }

    /// <summary>
    /// Validation + input loop for suffix text. Returns the validated
    /// text, or null if the user cancelled.
    /// </summary>
    private static string? PromptForSuffixText(string title, string prompt, string defaultValue)
    {
        while (true)
        {
            var dlg = new InputDialog(title, prompt, defaultValue) { Owner = null };
            if (dlg.ShowDialog() != true) return null;

            var text = dlg.Value.Trim();

            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Suffix cannot be empty.", title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }
            if (text.Length > 50)
            {
                MessageBox.Show("Suffix must be 50 characters or fewer.", title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }
            if (!text.All(char.IsLetterOrDigit))
            {
                MessageBox.Show(
                    "Suffix can only contain letters and numbers (a–z, A–Z, 0–9).\n\nNo spaces, brackets, or special characters.",
                    title, MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }

            return text;
        }
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
        while (d != null)
        {
            if (d is ListBoxItem lbi) return lbi;
            if (d is ListBox lb) return lb;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}
