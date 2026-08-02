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

    /// <param name="fileName">The colliding filename, for display.</param>
    /// <param name="incrementPreview">
    /// What Increment would produce, shown as a hint so the choice is not a
    /// guess.
    /// </param>
    public ConflictDialog(string fileName, string? incrementPreview = null)
    {
        InitializeComponent();
        FileNameText.Text = fileName;
        IncrementHint.Text = incrementPreview == null
            ? string.Empty
            : $"Increment would save as:  {incrementPreview}";
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
        // naming style and is reapplied by the caller.
        var current = System.IO.Path.GetFileNameWithoutExtension(FileNameText.Text);
        var bracket = current.LastIndexOf('[');
        if (bracket > 0) current = current[..bracket];

        var input = new InputDialog("Rename", "Enter new filename (no extension or suffix):", current)
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
