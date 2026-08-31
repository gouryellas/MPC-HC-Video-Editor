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

    /// <summary>
    /// True when the user asked not to be prompted for the rest of the batch.
    /// </summary>
    /// <remarks>
    /// It cannot mean "use this same name again" — every later file has a
    /// different name and they would all collide. It means "correct the rest
    /// automatically", i.e. take the suggested sanitisation for each without
    /// asking. The name typed here still applies to this file.
    /// </remarks>
    public bool ApplyToAll => ApplyToAllCheck.IsChecked == true;

    /// <param name="originalName">Filename with extension, for display.</param>
    /// <param name="stem">The renameable part, without suffix or extension.</param>
    /// <param name="suffix">Trailing bracket suffix, kept as-is.</param>
    /// <param name="extension">Extension including the dot, kept as-is.</param>
    /// <param name="reason">Why the rename is being asked for.</param>
    /// <param name="offerApplyToAll">
    /// Show the "do this for all remaining files" option. Only meaningful when
    /// more than one file is being processed.
    /// </param>
    public RenameFileDialog(string originalName, string stem, string suffix, string extension,
                            string reason, bool offerApplyToAll = false)
    {
        InitializeComponent();

        _suffix = suffix;
        _extension = extension;
        _suggested = FileNameRules.Sanitize(stem);

        ReasonText.Text = reason;
        OriginalText.Text = originalName;

        ApplyToAllCheck.Visibility = offerApplyToAll
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        // Always open on a name that would be accepted.
        NameBox.Text = FileNameRules.IsValid(stem) ? stem : _suggested;
        NameBox.CaretIndex = NameBox.Text.Length;
        NameBox.Focus();
    }

    /// <summary>
    /// Blocks disallowed characters at the keystroke, explaining the policy
    /// rather than silently swallowing the input — except for spaces, which
    /// are typed straight through as dashes.
    /// </summary>
    /// <remarks>
    /// Rejecting the space key made the box tell the user to press a
    /// different key than the one they meant, every time. Substituting
    /// matches what the automatic correction does to the incoming filename,
    /// so typing and importing agree.
    /// </remarks>
    private void NameBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Any(char.IsWhiteSpace))
        {
            e.Handled = true;

            // Substituted per character rather than through NormalizeSpaces:
            // that trims edge dashes, which would swallow the space entirely
            // when typed at the end of the name — exactly where a space is
            // usually typed.
            var substituted = new string(e.Text
                .Select(c => char.IsWhiteSpace(c) ? '-' : c)
                .ToArray());

            if (!substituted.All(FileNameRules.IsAllowedChar)) return;

            // Assigning SelectedText replaces a selection, or inserts at the
            // caret when there is none.
            var insertAt = NameBox.SelectionStart;
            NameBox.SelectedText = substituted;
            NameBox.CaretIndex = insertAt + substituted.Length;
            NameBox.SelectionLength = 0;
            return;
        }

        foreach (var c in e.Text)
        {
            if (FileNameRules.IsAllowedChar(c)) continue;

            e.Handled = true;
            MessageText.Text = $"'{c}' is not allowed.\n\n" + FileNameRules.Description;
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
            ? Services.ThemeService.Brush(nameof(Services.ThemePalette.StatusOk))
            : Services.ThemeService.Brush(nameof(Services.ThemePalette.StatusError));

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
