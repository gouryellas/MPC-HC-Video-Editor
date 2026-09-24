using System.Windows;

namespace MpcHcVideoEditor.Dialogs;

public enum ConflictResult { Overwrite, Increment, Rename, Cancel }

/// <summary>
/// Asked once per output file whose name is already taken. The caller keeps
/// showing this until the chosen name is free — see
/// <c>MainViewModel.ResolveOutputPathAsync</c>.
/// </summary>
public partial class ConflictDialog : Window
{
    public ConflictResult Result { get; private set; } = ConflictResult.Cancel;

    /// <summary>The new base name, without the bracket suffix or extension.</summary>
    public string? NewName { get; private set; }

    /// <summary>Where the output is going, for the Rename prompt to repeat.</summary>
    private readonly string? _directory;

    /// <summary>
    /// True when the user asked not to be prompted for the rest of the batch.
    /// </summary>
    /// <remarks>
    /// Only meaningful alongside Overwrite or Increment. A name typed into
    /// Rename cannot be reused on a later file — it would collide with itself
    /// — so the caller ignores this when the result is Rename.
    /// </remarks>
    public bool ApplyToAll => ApplyToAllCheck.IsChecked == true;

    /// <param name="fileName">The colliding filename, for display.</param>
    /// <param name="incrementPreview">
    /// What Increment would produce, shown as a hint so the choice is not a
    /// guess.
    /// </param>
    /// <param name="directory">
    /// The folder holding the colliding file, which is also where every one of
    /// these buttons writes. Overwrite replaces what is sitting there, so the
    /// dialog names it rather than leaving it to be assumed.
    /// </param>
    /// <param name="offerApplyToAll">
    /// Show the "do this for all remaining files" option. Only meaningful when
    /// more than one file is being processed.
    /// </param>
    public ConflictDialog(string fileName, string? incrementPreview = null,
                          string? directory = null, bool offerApplyToAll = false)
    {
        InitializeComponent();
        FileNameText.Text = fileName;
        IncrementHint.Text = incrementPreview == null
            ? string.Empty
            : $"Increment would save as:  {incrementPreview}";

        _directory = directory;

        if (string.IsNullOrWhiteSpace(directory))
        {
            FolderText.Visibility = Visibility.Collapsed;
        }
        else
        {
            FolderText.Text = $"in  {directory}";
            FolderText.ToolTip = directory;
        }

        ApplyToAllCheck.Visibility = offerApplyToAll ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Overwrite_Click(object sender, RoutedEventArgs e)
    {
        Result = ConflictResult.Overwrite;
        DialogResult = true;
    }

    private void Increment_Click(object sender, RoutedEventArgs e)
    {
        Result = ConflictResult.Increment;
        DialogResult = true;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        // Only the base name is editable — the bracket suffix is part of the
        // naming tag and is reapplied by the caller.
        var current = System.IO.Path.GetFileNameWithoutExtension(FileNameText.Text);
        var bracket = current.LastIndexOf('[');
        if (bracket > 0) current = current[..bracket];

        // The folder goes in the prompt too. This window names it, and the one
        // that opens on top of it covers that up — so the question "what am I
        // renaming this to, and where does it land" would lose half its answer
        // at the moment it is being answered.
        var prompt = string.IsNullOrWhiteSpace(_directory)
            ? "Enter new filename (no extension or suffix):"
            : $"Enter new filename (no extension or suffix).\nSaving into:  {_directory}";

        var input = new InputDialog("Rename", prompt, current)
        {
            Owner = this
        };

        if (input.ShowDialog() == true && !string.IsNullOrWhiteSpace(input.Value))
        {
            NewName = input.Value.Trim();
            Result = ConflictResult.Rename;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = ConflictResult.Cancel;
        DialogResult = false;
    }
}
