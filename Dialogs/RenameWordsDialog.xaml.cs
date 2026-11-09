using System.Windows;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// The words the bulk renamer recognises: categories, subjects and first
/// names, one per line.
/// </summary>
/// <remarks>
/// Three boxes of text rather than three managed lists. The content is a
/// handful of words a person keeps in their head anyway, and a text box can
/// be pasted into, sorted in an editor, and read at a glance — none of which
/// a grid of rows with add and remove buttons does better.
/// </remarks>
public partial class RenameWordsDialog : Window
{
    private readonly SettingsService _settings;

    public RenameWordsDialog(SettingsService settings)
    {
        InitializeComponent();

        _settings = settings;

        CategoriesBox.Text = string.Join(Environment.NewLine, settings.Current.RenameCategories);
        SubjectsBox.Text = string.Join(Environment.NewLine, settings.Current.RenameSubjects);
        FirstNamesBox.Text = string.Join(Environment.NewLine, settings.Current.RenameFirstNames);
    }

    /// <summary>
    /// Splits a box into words, cleaned the same way a filename is.
    /// </summary>
    /// <remarks>
    /// Through the filename rules, because these are matched against words
    /// taken out of a cleaned filename — a category typed as "Wedding" or
    /// "wedding party" would otherwise never match anything, and would fail
    /// silently, which is the worst way for a word list to be wrong.
    /// </remarks>
    private static List<string> Words(string? text) =>
        (text ?? "")
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => FilenameRules.Clean(line.Trim()))
        .Where(word => word.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(word => word, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.RenameCategories = Words(CategoriesBox.Text);
        _settings.Current.RenameSubjects = Words(SubjectsBox.Text);
        _settings.Current.RenameFirstNames = Words(FirstNamesBox.Text);

        _settings.Save();

        DialogResult = true;
        Close();
    }
}
