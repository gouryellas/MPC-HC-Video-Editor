using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Help ▸ About: version, licensing, and a way to reach the repository.
/// </summary>
public partial class AboutDialog : Window
{
    /// <summary>Canonical repository URL, also used by Help ▸ GitHub repository.</summary>
    public const string RepositoryUrl = "https://github.com/gouryellas/MPC-HC-Video-Editor";

    public AboutDialog()
    {
        InitializeComponent();

        var asm = Assembly.GetExecutingAssembly();

        // InformationalVersion carries the full string including any suffix;
        // AssemblyVersion is the padded four-part form and reads oddly here.
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "unknown";

        // Strip the source-control hash MSBuild appends to
        // InformationalVersion — "3.0.0+9a1b2c3" reads as noise here.
        var plus = version.IndexOf('+');
        if (plus > 0) version = version[..plus];

        VersionText.Text = $"Version {version}";

        LicenseText.Text = ReadLicense();
    }

    /// <summary>
    /// Shows the shipped LICENSE file when there is one, and says plainly that
    /// there isn't when there isn't.
    /// </summary>
    /// <remarks>
    /// Naming a licence the project has not actually declared would be worse
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
                // An unreadable licence file is not worth failing the dialog over.
            }
        }

        return "No licence file is installed alongside this application. " +
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
