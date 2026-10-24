using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Scans a video for clip boundaries and lets the user accept or reject what
/// it found.
/// </summary>
/// <remarks>
/// Two phases in one window: choose what to look for, scan, then review. The
/// proposals are never applied by scanning — detection on real footage is a
/// good first guess and no more, so the list is something to correct rather
/// than something that happens to you.
/// </remarks>
public partial class DetectBookmarksDialog : Window
{
    /// <summary>One proposal, with whether the user wants it.</summary>
    public sealed class Proposal : INotifyPropertyChanged
    {
        private bool _use = true;

        public required DetectedRange Range { get; init; }
        public required string Display { get; init; }

        public bool Use
        {
            get => _use;
            set { _use = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Use))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly FFmpegService _ffmpeg;
    private readonly string _videoPath;
    private readonly ObservableCollection<Proposal> _proposals = new();

    /// <summary>
    /// Where this dialog's progress goes while it is up.
    /// </summary>
    /// <remarks>
    /// The dialog is modal, so the window behind it is visible and idle for the
    /// whole of a scan — and a scan is the longest thing either menu entry
    /// does. Reporting through to the status bar is the only way anything is
    /// said there at all, since the command that opened this is blocked inside
    /// ShowDialog until it closes.
    /// </remarks>
    public Action<string>? Report { get; init; }

    /// <summary>The ranges the user accepted, in order.</summary>
    public List<DetectedRange> Accepted { get; private set; } = new();

    /// <summary>
    /// Every range the last scan proposed, whether or not it was accepted.
    /// </summary>
    /// <remarks>
    /// The edges of these are where the video actually changes, which is
    /// useful to the timeline's bracket snapping regardless of what the user
    /// did with the proposals — a scan that was reviewed and rejected still
    /// found the boundaries. Readable after Cancel for the same reason.
    /// </remarks>
    public List<DetectedRange> Scanned => _proposals.Select(p => p.Range).ToList();

    /// <summary>Whether the accepted ranges replace the current bookmarks.</summary>
    public bool ReplaceExisting { get; private set; }

    /// <param name="hasExistingBookmarks">
    /// Whether there is a cut list to replace. With none, the question does not
    /// arise — "Replace the bookmarks currently loaded" asked about nothing, and
    /// a scan on a video with an empty list is the ordinary first use of this
    /// window rather than an edge case.
    /// </param>
    public DetectBookmarksDialog(FFmpegService ffmpeg, string videoPath, bool hasExistingBookmarks = false)
    {
        InitializeComponent();
        _ffmpeg = ffmpeg;
        _videoPath = videoPath;
        ResultsList.ItemsSource = _proposals;
        ApplyDefaultsForMode();

        if (!hasExistingBookmarks)
        {
            ReplaceCheck.IsChecked = false;
            ReplaceCheck.Visibility = Visibility.Collapsed;
        }

        // Nothing is proposed yet, so there is nothing to accept. From here on
        // the button follows what is ticked and nothing else.
        _proposals.CollectionChanged += (_, _) => RefreshUseButton();
        RefreshUseButton();
    }

    /// <summary>
    /// "Use selected" is live exactly when something is selected.
    /// </summary>
    /// <remarks>
    /// Its enabled state used to be driven by the scan — switched off when one
    /// started and back on by its result — which made it flicker through the
    /// disabled look for reasons that had nothing to do with whether there was
    /// anything to use. It now answers only its own question, so a scan running
    /// over a list the user has already ticked leaves it alone.
    /// </remarks>
    private void RefreshUseButton() =>
        UseButton.IsEnabled = _proposals.Any(p => p.Use);

    private DetectionMode SelectedMode =>
        ModeBlack.IsChecked == true ? DetectionMode.BlackFrames
        : ModeScene.IsChecked == true ? DetectionMode.SceneChanges
        : DetectionMode.Silence;

    /// <summary>
    /// Loads the defaults for the chosen mode. The three thresholds measure
    /// entirely different things, so carrying a number across modes would be
    /// meaningless rather than convenient.
    /// </summary>
    private void ApplyDefaultsForMode()
    {
        var mode = SelectedMode;
        var d = DetectionSettings.For(mode);

        ThresholdBox.Text = d.Threshold.ToString("0.##", CultureInfo.CurrentCulture);
        GapBox.Text = d.MinBoundarySeconds.ToString("0.##", CultureInfo.CurrentCulture);
        MinClipBox.Text = d.MinClipSeconds.ToString("0.##", CultureInfo.CurrentCulture);

        (ThresholdLabel.Text, ThresholdHint.Text) = mode switch
        {
            DetectionMode.BlackFrames => ("Blackness",
                "How black a frame must be, from 0 to 1. 0.98 catches proper fades without treating a dark shot as a gap."),
            DetectionMode.SceneChanges => ("Sensitivity",
                "How different two frames must be, from 0 to 1. Lower finds more cuts. Around 0.3 suits most edited footage; a real hard cut often scores about 0.4."),
            _ => ("Silence (dB)",
                "How quiet counts as silence, in dBFS. −30 suits a normal room; −45 is stricter and needs a clean recording.")
        };

        // A gap has no meaning for scene cuts — a cut is instantaneous.
        GapBox.IsEnabled = mode != DetectionMode.SceneChanges;
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (ThresholdBox is null) return;   // fires during InitializeComponent
        ApplyDefaultsForMode();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(ThresholdBox, out var threshold) ||
            !TryRead(GapBox, out var gap) ||
            !TryRead(MinClipBox, out var minClip)) return;

        var settings = new DetectionSettings(SelectedMode, threshold, Math.Max(0, gap), Math.Max(0, minClip));

        ScanButton.IsEnabled = false;
        _proposals.Clear();
        ScanStatus.Text = "Scanning…";
        Report?.Invoke("Scanning…");

        // Says what it is doing while it does it. A disabled button that still
        // read "Scan video" looked like a button that had refused the click,
        // and the colours are swapped with the words because the point of the
        // pair is to be legible together — black on the pale fill, rather than
        // the white-on-white the accent foreground left behind.
        ScanButton.Content = "Scanning…";
        ScanButton.Background = Brushes.Gainsboro;
        ScanButton.Foreground = Brushes.Black;

        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                if (!string.IsNullOrEmpty(p.Message)) ScanStatus.Text = p.Message;
            });

            var found = await _ffmpeg.DetectRangesAsync(_videoPath, settings, progress);

            int n = 1;
            foreach (var r in found)
            {
                var proposal = new Proposal
                {
                    Range = r,
                    Display = $"{n++}.   {Bookmark.FormatTime(r.Start)}  →  {Bookmark.FormatTime(r.End)}" +
                              $"   ({r.Duration:0.#}s)"
                };
                proposal.PropertyChanged += (_, _) => RefreshUseButton();
                _proposals.Add(proposal);
            }

            ResultsHeading.Text = $"Proposed clips ({found.Count})";
            ScanStatus.Text = found.Count == 0
                ? "Nothing found — try a lower threshold or a shorter minimum clip."
                : $"Found {found.Count}. Untick anything you do not want.";
            Report?.Invoke(CompletionMessage(found.Count));
        }
        catch (Exception ex)
        {
            ScanStatus.Text = "Scan failed.";
            Report?.Invoke("Scan failed.");
            MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            ScanButton.IsEnabled = true;
            ScanButton.Content = "Scan video";
            // Back to the resource references the XAML set, not to fixed
            // brushes frozen out of the theme that was current a minute ago.
            ScanButton.SetResourceReference(BackgroundProperty, "PrimaryButton");
            ScanButton.SetResourceReference(ForegroundProperty, "OnAccent");
        }
    }

    /// <summary>What the status bar says once a scan is over.</summary>
    public static string CompletionMessage(int count) => count == 0
        ? "Scan didn't find any cuts — try a lower threshold or another mode."
        : $"Scan completed. Found {count} {(count == 1 ? "cut" : "cuts")}";

    private bool TryRead(System.Windows.Controls.TextBox box, out double value)
    {
        if (double.TryParse(box.Text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;

        MessageBox.Show(this, "That needs to be a number.", "Scan for cuts",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        box.Focus();
        box.SelectAll();
        return false;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _proposals) p.Use = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _proposals) p.Use = false;
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        Accepted = _proposals.Where(p => p.Use).Select(p => p.Range).ToList();
        if (Accepted.Count == 0)
        {
            MessageBox.Show(this, "Nothing is ticked, so there is nothing to add.", "Scan for cuts",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ReplaceExisting = ReplaceCheck.IsChecked == true;
        DialogResult = true;
    }
}
