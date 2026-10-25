using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks which drive the missing files are on now.
/// </summary>
/// <remarks>
/// A list of drives rather than a folder browser, because the question this
/// answers is usually "the disk came back on a different letter" — and a folder
/// browser makes you navigate into a drive to name it, having first found which
/// of them is the right one with no help from the dialog. Here the labels and
/// sizes are on screen, which is what identifies an external disk.
///
/// The folder browser is still one click away. Files that moved rather than
/// files whose letter changed are the other half of the problem, and pointing
/// straight at the folder they moved into beats searching for them.
/// </remarks>
public partial class DriveSelectDialog : Window
{
    /// <summary>One drive, as the list draws it.</summary>
    public sealed record DriveChoice(string Root, string Label, string Kind, string Space);

    /// <summary>The drive root or folder chosen. Empty until one is.</summary>
    public string SelectedPath { get; private set; } = string.Empty;

    /// <summary>
    /// Whether <see cref="SelectedPath"/> is a whole drive rather than a folder
    /// picked through the browser.
    /// </summary>
    /// <remarks>
    /// The two are searched differently — a drive is first tried as a straight
    /// re-lettering of the old paths, which means nothing for a folder — so the
    /// caller has to be able to tell them apart, and a trailing backslash is
    /// not a reliable way to.
    /// </remarks>
    public bool IsWholeDrive { get; private set; }

    public DriveSelectDialog(string prompt)
    {
        InitializeComponent();
        Prompt.Text = prompt;
        DriveList.ItemsSource = ReadyDrives();
        DriveList.SelectionChanged += (_, _) =>
            SearchButton.IsEnabled = DriveList.SelectedItem is DriveChoice;
    }

    /// <summary>
    /// Every attached, readable drive.
    /// </summary>
    /// <remarks>
    /// <c>IsReady</c> filters out the empty card readers and disconnected
    /// network mappings that <c>GetDrives</c> otherwise lists, and asking a
    /// drive for its label or free space is what throws when it is one of
    /// those — so each is read inside the try that decides whether to list it
    /// at all.
    /// </remarks>
    private static List<DriveChoice> ReadyDrives()
    {
        var drives = new List<DriveChoice>();

        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;

                var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "Local disk" : d.VolumeLabel;
                var kind = d.DriveType switch
                {
                    DriveType.Removable => "Removable drive",
                    DriveType.Network => "Network drive",
                    DriveType.CDRom => "Optical drive",
                    DriveType.Ram => "RAM disk",
                    _ => "Fixed drive"
                };

                drives.Add(new DriveChoice(d.Name, label, kind,
                    $"{Gigabytes(d.AvailableFreeSpace)} free of {Gigabytes(d.TotalSize)}"));
            }
            catch
            {
                // Not ready after all, between the check and the question.
            }
        }

        return drives;
    }

    private static string Gigabytes(long bytes) => $"{bytes / 1024d / 1024d / 1024d:0.#} GB";

    private void DriveList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DriveList.SelectedItem is DriveChoice) Search_Click(sender, e);
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (DriveList.SelectedItem is not DriveChoice choice) return;

        SelectedPath = choice.Root;
        IsWholeDrive = true;
        DialogResult = true;
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Which folder should be searched?" };
        if (picker.ShowDialog(this) != true) return;

        SelectedPath = picker.FolderName;
        IsWholeDrive = false;
        DialogResult = true;
    }
}
