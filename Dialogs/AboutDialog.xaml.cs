using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Help ▸ About: version, licensing, and a way to reach the repository.
/// </summary>
public partial class AboutDialog : Window
{
    /// <summary>Canonical repository URL, also used by Help ▸ GitHub repository.</summary>
    /// <remarks>
    /// Defined in <see cref="UpdateCheckService"/>, which needs the same base
    /// to reach the API and cannot depend on this layer. Kept here so the
    /// existing callers read the same as they always did.
    /// </remarks>
    public const string RepositoryUrl = UpdateCheckService.RepositoryUrl;

    public AboutDialog()
    {
        InitializeComponent();

        VersionText.Text = $"Version {AppVersion.Display}";

        LicenseText.Text = ReadLicense();

        // Drawn at 128 and shown at 56: the mark is vector, and asking for it
        // at the size it will be displayed leaves nothing in hand for a
        // high-DPI screen, where WPF draws those 56 points across ninety-odd
        // pixels.
        try { Logo.Source = IconRenderer.At(ThemeService.Current, 128); }
        catch { /* a missing picture is not worth failing the dialog over */ }

        BuildVersionHistory();
    }

    /// <summary>
    /// Lists every released version, newest first, each opening onto its own
    /// notes.
    /// </summary>
    /// <remarks>
    /// Only the rows are built here. The notes behind each one are laid out
    /// the first time it is opened and kept thereafter — forty-odd versions
    /// of bullets is a few thousand text blocks, and building them all to
    /// show a window whose first screen holds eight rows would make Help ▸
    /// About the slowest dialog in the program for something nobody had
    /// asked to read yet.
    /// </remarks>
    private void BuildVersionHistory()
    {
        var entries = ChangelogService.Entries;

        if (entries.Count == 0)
        {
            HistoryEmpty.Text = "The changelog could not be read from this build.";
            HistoryEmpty.Visibility = Visibility.Visible;
            return;
        }

        HistoryCount.Text = $"{entries.Count} releases";

        var style = (Style)FindResource("VersionExpander");
        var current = AppVersion.Display;

        foreach (var entry in entries)
        {
            var expander = new Expander
            {
                Style = style,
                Header = BuildHeader(entry, isCurrent: entry.Version == current),
                Tag = entry
            };

            expander.Expanded += Version_Expanded;
            VersionList.Children.Add(expander);
        }
    }

    /// <summary>The version and its date, with the running one marked.</summary>
    private UIElement BuildHeader(ChangelogEntry entry, bool isCurrent)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var version = new TextBlock
        {
            Text = entry.Version,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        version.SetResourceReference(ForegroundProperty, "TextPrimary");
        row.Children.Add(version);

        if (!string.IsNullOrEmpty(entry.Released))
        {
            var date = new TextBlock
            {
                Text = entry.Released,
                FontSize = 11,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            date.SetResourceReference(ForegroundProperty, "TextMuted");
            row.Children.Add(date);
        }

        // Which one you are running, said here rather than left to be worked
        // out by comparing the heading at the top of the window against a
        // list of forty.
        if (isCurrent)
        {
            var badge = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 0, 5, 1),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.SetResourceReference(Border.BackgroundProperty, "ControlBackground");

            var text = new TextBlock { Text = "installed", FontSize = 10 };
            text.SetResourceReference(ForegroundProperty, "ValueGreen");

            badge.Child = text;
            row.Children.Add(badge);
        }

        return row;
    }

    /// <summary>Lays out one version's notes, once.</summary>
    private void Version_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander || expander.Content is not null) return;
        if (expander.Tag is not ChangelogEntry entry) return;

        var panel = new StackPanel();

        foreach (var block in NoteRenderer.Render(entry.Notes))
            panel.Children.Add(block);

        expander.Content = panel;
    }

    /// <summary>
    /// Shows the shipped LICENSE file when there is one, and says plainly that
    /// there isn't when there isn't.
    /// </summary>
    /// <remarks>
    /// Naming a license the project has not actually declared would be worse
    /// than saying nothing — it is the one claim in this dialog a user might
    /// rely on.
    /// </remarks>
    private static string ReadLicense()
    {
        foreach (var name in new[] { "LICENSE", "LICENSE.txt", "LICENSE.md", "COPYING" })
        {
            try
            {
                var path = Path.Combine(PortablePaths.AppFolder, name);
                if (File.Exists(path)) return File.ReadAllText(path).Trim();
            }
            catch
            {
                // An unreadable license file is not worth failing the dialog over.
            }
        }

        return "No license file is installed alongside this application. " +
               "See the repository for the terms this project is released under.";
    }

    private void Repo_Click(object sender, RoutedEventArgs e) => OpenUrl(RepositoryUrl);

    /// <summary>
    /// Hands a URL to the default browser.
    /// </summary>
    /// <remarks>
    /// <c>UseShellExecute</c> is required: without it .NET tries to execute the
    /// URL as a program and throws.
    /// </remarks>
    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the link:\n\n{url}\n\n{ex.Message}",
                "Open link", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
