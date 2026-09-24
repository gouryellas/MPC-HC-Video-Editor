using System.Windows;
using System.Windows.Input;

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
    /// <param name="highlights">
    /// Short lines from the release's notes. Optional, and an empty list takes
    /// the whole section off the window — see
    /// <see cref="Services.UpdateCheckService.Highlights"/>.
    /// </param>
    public UpdateAvailableDialog(string latestVersion, string runningVersion, string releaseUrl,
                                 IReadOnlyList<string>? highlights = null)
    {
        InitializeComponent();

        _releaseUrl = releaseUrl;

        HeadlineText.Text = string.IsNullOrWhiteSpace(latestVersion)
            ? "A newer version is available"
            : $"Version {latestVersion} is available";

        RunningText.Text = $"You are running {runningVersion}.";

        if (highlights is { Count: > 0 })
        {
            // Named rather than a bare "What's new", so it is clear these lines
            // describe the release being offered and not this build.
            ChangesHeading.Text = string.IsNullOrWhiteSpace(latestVersion)
                ? "WHAT CHANGED"
                : $"WHAT CHANGED IN {latestVersion}";

            ChangesList.ItemsSource = highlights;
        }
        else
        {
            ChangesPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void Releases_Click(object sender, RoutedEventArgs e)
    {
        AboutDialog.OpenUrl(_releaseUrl);
        Close();
    }

    /// <summary>
    /// Dismisses the notice, leaving the update check on.
    /// </summary>
    /// <remarks>
    /// An explicit Close, because <c>IsCancel</c> cannot do it here: it works
    /// by setting <see cref="Window.DialogResult"/>, which throws on — and is
    /// therefore skipped for — a window that was not shown with
    /// <c>ShowDialog</c>. This one is shown with <c>Show</c>, so the button
    /// raised its Click and nothing happened.
    /// </remarks>
    private void NotNow_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Closes on Esc.
    /// </summary>
    /// <remarks>
    /// Handled here for the same reason as <see cref="NotNow_Click"/>. Esc
    /// reaches the <c>IsCancel</c> button through the same DialogCancel path
    /// that a click does, so it was dead in exactly the same way — and a notice
    /// nobody asked for is precisely the kind of window Esc should shift.
    /// </remarks>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
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
