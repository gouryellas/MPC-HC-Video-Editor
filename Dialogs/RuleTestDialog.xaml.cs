using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>One file this rule changes, and what it changes it to.</summary>
public sealed class RuleTestRow
{
    public RuleTestRow(string original, string result)
    {
        Original = original;
        Result = result;
    }

    public string Original { get; }
    public string Result { get; }
}

/// <summary>
/// What one rule does to the files that are actually there.
/// </summary>
/// <remarks>
/// <para>
/// Against the folder set in the rename window rather than against made-up
/// names, which could only ever be a guess at what somebody's files look
/// like — and a guess that always matched the rule, since the examples were
/// built out of the rule's own trigger word.
/// </para>
/// <para>
/// The whole vocabulary is used, not the one rule in isolation: what matters
/// is what the renamer will actually do, and that depends on the other lists
/// and rules as well. What is listed is the difference this one rule makes —
/// every file whose new name would be something else if this rule were not
/// there. A rule that quietly never fires is the thing worth finding, and it
/// is invisible in a list of every rename the folder would get.
/// </para>
/// <para>
/// Nothing here is editable and nothing is renamed. It reports.
/// </para>
/// </remarks>
public partial class RuleTestDialog : Window
{
    private readonly ObservableCollection<RuleTestRow> _rows = new();

    public RuleTestDialog(RenameRule rule, FilenameVocabulary vocabulary,
                          IReadOnlyList<string> files)
    {
        InitializeComponent();

        ResultList.ItemsSource = _rows;
        RuleText.Text = rule.Description;

        Run(rule, vocabulary, files);
    }

    /// <summary>
    /// Runs the folder twice — with the rule and without it — and keeps the
    /// names that come out differently.
    /// </summary>
    private void Run(RenameRule rule, FilenameVocabulary vocabulary,
                     IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            Summary("No files to check. Choose a folder in the rename window first.");
            return;
        }

        var without = new FilenameVocabulary
        {
            Words = vocabulary.Words,
            Blacklist = vocabulary.Blacklist,
            Rules = vocabulary.Rules.Where(r => !ReferenceEquals(r, rule)).ToList()
        };

        // Grouped by folder, the same way the rename preview groups them: a
        // clash is only a clash inside one folder.
        foreach (var folder in files.GroupBy(f => Path.GetDirectoryName(f) ?? ""))
        {
            var names = folder.Select(Path.GetFileName).OfType<string>()
                              .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                              .ToList();

            foreach (var name in names)
            {
                var withRule = FilenameRules.Apply(name, vocabulary);
                var plain = FilenameRules.Apply(name, without);

                if (!string.Equals(withRule, plain, StringComparison.Ordinal))
                    _rows.Add(new RuleTestRow(name, withRule));
            }
        }

        Summary(_rows.Count == 0
            ? $"This rule changes nothing. {Count(files.Count)} checked."
            : $"{Count(_rows.Count)} changed by this rule, out of {files.Count} checked.");
    }

    private static string Count(int n) => n == 1 ? "1 file" : $"{n} files";

    /// <summary>Whether the rule changed any file at all.</summary>
    /// <remarks>
    /// Asked by the opener before showing this window: a test that found
    /// nothing is one sentence, and one sentence is a toast, not a
    /// dialog to read and dismiss.
    /// </remarks>
    public bool FoundAnything => _rows.Count > 0;

    /// <summary>The one-line verdict, for saying outside this window.</summary>
    public string Outcome { get; private set; } = "";

    private void Summary(string text)
    {
        Outcome = text;
        SummaryText.Text = text;
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

}
