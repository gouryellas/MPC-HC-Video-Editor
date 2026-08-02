using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks for a filename that satisfies <see cref="FileNameRules"/>. The field
/// is pre-filled with a valid name — the original when it already passes, the
/// sanitized suggestion when it does not — and rejects disallowed characters
/// as they are typed rather than only complaining on OK.
/// </summary>
public partial class RenameFileDialog : Window
{
    private readonly string _suffix;
    private readonly string _extension;
    private readonly string _suggested;

    /// <summary>The accepted stem, without suffix or extension.</summary>
    public string? NewStem { get; private set; }

    /// <param name="originalName">Filename with extension, for display.</param>
    /// <param name="stem">The renameable part, without suffix or extension.</param>
    /// <param name="suffix">Trailing bracket suffix, kept as-is.</param>
    /// <param name="extension">Extension including the dot, kept as-is.</param>
    /// <param name="reason">Why the rename is being asked for.</param>
    public RenameFileDialog(string originalName, string stem, string suffix, string extension, string reason)
    {
        InitializeComponent();

        _suffix = suffix;
        _extension = extension;
        _suggested = FileNameRules.Sanitize(stem);

        ReasonText.Text = reason;
        OriginalText.Text = originalName;

        // Always open on a name that would be accepted.
        NameBox.Text = FileNameRules.IsValid(stem) ? stem : _suggested;
        NameBox.CaretIndex = NameBox.Text.Length;
        NameBox.Focus();
    }

    /// <summary>
    /// Blocks disallowed characters at the keystroke, explaining the policy
    /// rather than silently swallowing the input.
    /// </summary>
    private void NameBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var c in e.Text)
        {
            if (FileNameRules.IsAllowedChar(c)) continue;

            e.Handled = true;
            MessageText.Text = c == ' '
                ? "Spaces are not allowed — use a dash instead.\n\n" + FileNameRules.Description
                : $"'{c}' is not allowed.\n\n" + FileNameRules.Description;
            return;
        }
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Doubled punctuation can still arrive by paste or by deleting the
        // character between two of them, so validate the whole string too.
        var stem = NameBox.Text;
        var ok = FileNameRules.IsValid(stem);

        PreviewText.Text = $"Saves as:  {stem}{_suffix}{_extension}";
        PreviewText.Foreground = ok
            ? System.Windows.Media.Brushes.MediumAquamarine
            : System.Windows.Media.Brushes.Salmon;

        if (ok) MessageText.Text = string.Empty;
        else if (!string.IsNullOrEmpty(stem))
            MessageText.Text = "That name is not accepted yet.\n\n" + FileNameRules.Description;
    }

    private void Suggested_Click(object sender, RoutedEventArgs e)
    {
        NameBox.Text = _suggested;
        NameBox.CaretIndex = NameBox.Text.Length;
        NameBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var stem = NameBox.Text;
        if (!FileNameRules.IsValid(stem))
        {
            MessageText.Text = "Cannot continue with that name.\n\n" + FileNameRules.Description;
            return;
        }

        NewStem = stem;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
