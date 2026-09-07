using System.Windows;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Says that a newer release exists, and offers the two things a user can do
/// about it: go and get it, or stop being told.
/// </summary>
/// <remarks>
/// Shown non-modally. The startup check finishes on its own schedule, and by
/// then the main window may be hidden behind the overlay or sitting in the
/// notification area — a modal dialog owned by a window in that state is a
/// dialog the user cannot get to. Nothing here needs an answer before the
/// program can carry on, so nothing blocks.
///
/// The window records what was chosen and lets the caller act on it, in
/// keeping with the settings dialog: writing to disk stays in one place rather
/// than being done by whichever window happened to be open.
/// </remarks>
public partial class UpdateAvailableDialog : Window
{
    /// <summary>
    /// Set when the user asked for the startup check to stop. Read by the
    /// caller once the window has closed.
    /// </summary>
    public bool StopChecking { get; private set; }

    private readonly string _releaseUrl;

    /// <param name="latestVersion">Published version, e.g. <c>"4.5"</c>.</param>
    /// <param name="runningVersion">This build's version.</param>
    /// <param name="releaseUrl">Page the buttons open.</param>
    public UpdateAvailableDialog(string latestVersion, string runningVersion, string releaseUrl)
    {
        InitializeComponent();

        _releaseUrl = releaseUrl;

        HeadlineText.Text = string.IsNullOrWhiteSpace(latestVersion)
            ? "A newer version is available"
            : $"Version {latestVersion} is available";

        RunningText.Text = $"You are running {runningVersion}.";
    }

    private void Releases_Click(object sender, RoutedEventArgs e)
    {
        AboutDialog.OpenUrl(_releaseUrl);
        Close();
    }

    /// <summary>
    /// Records the opt-out and closes. The caller persists it — see the class
    /// remarks.
    /// </summary>
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopChecking = true;
        Close();
    }
}
