using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Shows what one or more image files say about themselves.
/// </summary>
/// <remarks>
/// A window rather than a panel in the main view: this is something you open,
/// read and close, and it has nothing to do with the cut list the main window
/// is for.
/// </remarks>
public partial class ImageInfoDialog : Window
{
    private readonly IReadOnlyList<ImageFacts> _files;

    public ImageInfoDialog(IReadOnlyList<ImageFacts> files)
    {
        InitializeComponent();

        _files = files;

        if (files.Count > 1)
        {
            Files.ItemsSource = files.Select(f => f.FileName).ToList();
            Files.Visibility = Visibility.Visible;
            Files.SelectedIndex = 0;

            // Copy means all of them when several are open, because picking
            // twelve files and getting one file's worth of text back is not
            // what was asked for.
            CopyButton.Content = $"Copy all {files.Count}";
        }

        Show(files.Count > 0 ? files[0] : null);
    }

    private void Show(ImageFacts? facts)
    {
        if (facts is null) return;

        FileName.Text = facts.FileName;
        Groups.ItemsSource = facts.Groups;

        LocationNote.Visibility = facts.Location is null ? Visibility.Collapsed : Visibility.Visible;
        LocationText.Text = facts.Location ?? string.Empty;

        ProblemNote.Visibility = facts.Problem is null ? Visibility.Collapsed : Visibility.Visible;
        ProblemText.Text = facts.Problem ?? string.Empty;
    }

    private void Files_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Files.SelectedIndex >= 0 && Files.SelectedIndex < _files.Count)
            Show(_files[Files.SelectedIndex]);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var text = _files.Count > 1
            ? string.Join(Environment.NewLine + Environment.NewLine + new string('-', 40) + Environment.NewLine,
                          _files.Select(f => f.AsText()))
            : _files.Count == 1 ? _files[0].AsText() : string.Empty;

        try
        {
            Clipboard.SetText(text);
            CopyButton.Content = "Copied";
        }
        catch
        {
            // The clipboard is shared, and another application can be holding
            // it open. Nothing is lost by saying so and letting them try again.
            CopyButton.Content = "Clipboard busy";
        }
    }

    /// <summary>The row a button inside the list belongs to.</summary>
    private static MetaRow? RowOf(object sender) =>
        (sender as FrameworkElement)?.Tag as MetaRow;

    /// <summary>Puts a data row's whole value on the clipboard.</summary>
    private void CopyData_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || RowOf(sender) is not { Full: { } data }) return;

        try
        {
            Clipboard.SetText(data);
            button.Content = "Copied";
        }
        catch
        {
            button.Content = "Clipboard busy";
        }
    }

    /// <summary>
    /// Writes a data row's value out as a .json file.
    /// </summary>
    /// <remarks>
    /// Named after the picture and the chunk it came from, so a folder of them
    /// stays sorted alongside the images and a workflow is never confused with
    /// the one from the file opened before it. Written as UTF-8 without a byte
    /// order mark, which is what every JSON reader expects.
    /// </remarks>
    private void SaveData_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || RowOf(sender) is not { } row || row.Full is not { } data) return;

        var stem = System.IO.Path.GetFileNameWithoutExtension(FileName.Text);

        var save = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON|*.json|All Files|*.*",
            FileName = $"{stem}-{row.Name}.json",
            Title = $"Save the {row.Name} as JSON",
            AddExtension = true,
            DefaultExt = ".json"
        };

        if (save.ShowDialog(this) != true) return;

        try
        {
            System.IO.File.WriteAllText(save.FileName, data, new System.Text.UTF8Encoding(false));
            button.Content = "Saved";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
