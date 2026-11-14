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

    /// <summary>
    /// Whether dragged brackets should land on the points this scan found.
    /// </summary>
    /// <remarks>
    /// Read whichever way the dialog closes, Cancel included: the scan has
    /// still run by then and its marks are still kept, so the answer to
    /// "snap to them" is still live. <see cref="Scanned"/> is read on the same
    /// terms.
    /// </remarks>
    public bool SnapToPoints => SnapCheck.IsChecked == true;

    /// <param name="hasExistingBookmarks">
    /// Whether there is a cut list to replace. With none, the question does not
    /// arise — "Replace the bookmarks currently loaded" asked about nothing, and
    /// a scan on a video with an empty list is the ordinary first use of this
    /// window rather than an edge case.
    /// </param>
    public DetectBookmarksDialog(FFmpegService ffmpeg, string videoPath, bool hasExistingBookmarks = false,
                                 bool snapToPoints = false)
    {
        InitializeComponent();
        _ffmpeg = ffmpeg;
        _videoPath = videoPath;
        SnapCheck.IsChecked = snapToPoints;
        ResultsList.ItemsSource = _proposals;
        ApplyDefaultsForMode();

        // The name, with the whole path on hover: the line names what is
        // about to be read, and a full path would crowd out the button
        // beside it.
        VideoText.Text = System.IO.Path.GetFileName(videoPath);
        VideoText.ToolTip = videoPath;

        _tick.Tick += (_, _) => ShowWorking();

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
    private void RefreshUseButton()
    {
        var selected = _proposals.Count(p => p.Use);
        UseButton.IsEnabled = selected > 0;

        // The one line that says it all, in the place the progress line
        // was — the same slot the other two scan windows finish in, and
        // kept current as clips are ticked rather than written once.
        if (_finished)
        {
            Say("",
                $"Finished! Found {_proposals.Count} clip(s).   {selected} selected",
                _tookTime);
        }
    }

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

        // What each mode is for. The radio buttons carry only the name,
        // because the rail is a column and a sentence apiece would wrap
        // three times over — so the sentence for whichever is chosen
        // goes here instead.
        ModeHint.Text = mode switch
        {
            DetectionMode.BlackFrames => "For recordings that fade between segments.",
            DetectionMode.SceneChanges => "For edited footage with hard cuts.",
            _ => "For anything speech-driven."
        };

        // A gap has no meaning for scene cuts — a cut is instantaneous.
        GapBox.IsEnabled = mode != DetectionMode.SceneChanges;
        GapLabel.Foreground = (Brush)FindResource(
            mode != DetectionMode.SceneChanges ? "TextSecondary" : "TextMuted");
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (ThresholdBox is null) return;   // fires during InitializeComponent
        ApplyDefaultsForMode();
    }

    private CancellationTokenSource? _running;

    /// <summary>Whether a scan is going, which is what the button means.</summary>
    private bool _scanning;

    /// <summary>Drives the moving dots and the elapsed time.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _tick = new()
    {
        Interval = TimeSpan.FromMilliseconds(450)
    };

    private readonly System.Diagnostics.Stopwatch _clock = new();
    private int _dots;

    /// <summary>ffmpeg's latest word on how far through the video it is.</summary>
    private string _position = "";

    /// <summary>What the finished scan found, for the status line.</summary>
    private bool _finished;
    private string _tookTime = "";

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        // The same button, mid-scan, is the stop button.
        if (_scanning)
        {
            _running?.Cancel();
            return;
        }

        if (!TryRead(ThresholdBox, out var threshold) ||
            !TryRead(GapBox, out var gap) ||
            !TryRead(MinClipBox, out var minClip)) return;

        var settings = new DetectionSettings(SelectedMode, threshold, Math.Max(0, gap), Math.Max(0, minClip));

        _running?.Cancel();
        _running = new CancellationTokenSource();
        var ct = _running.Token;

        _proposals.Clear();
        _finished = false;
        _position = "";

        ShowScanning(true);
        EmptyText.Visibility = Visibility.Collapsed;

        ProgressPanel.Visibility = Visibility.Visible;
        _dots = 0;
        _clock.Restart();
        _tick.Start();
        ShowWorking();
        Report?.Invoke("Scanning…");

        try
        {
            // Held and painted on the timer's beat rather than on every
            // report: ffmpeg speaks far faster than anybody reads.
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                if (!string.IsNullOrEmpty(p.Message)) _position = p.Message;
            });

            var found = await _ffmpeg.DetectRangesAsync(_videoPath, settings, progress, ct);

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

            _tookTime = Elapsed;
            _finished = true;
            RefreshUseButton();

            if (found.Count == 0)
            {
                EmptyText.Text = "Nothing found — try a lower threshold or a shorter minimum clip.";
                EmptyText.Visibility = Visibility.Visible;
            }

            Report?.Invoke(CompletionMessage(found.Count));
        }
        catch (OperationCanceledException)
        {
            Say("", "Stopped.", Elapsed);
            Report?.Invoke("Scan stopped.");

            if (_proposals.Count == 0)
            {
                EmptyText.Text = "Stopped before anything was found.";
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Say("", "The scan could not finish.", Elapsed);
            Report?.Invoke("Scan failed.");

            EmptyText.Text = "The scan could not finish: " + ex.Message;
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            _tick.Stop();
            _clock.Stop();
            ShowScanning(false);
        }
    }

    /// <summary>The scan's own clock, written the way the status bar writes one.</summary>
    private string Elapsed => JobProgress.FormatElapsed(_clock.Elapsed);

    /// <summary>
    /// Turns the one button into whichever of its two jobs applies.
    /// </summary>
    /// <remarks>
    /// A scan used to grey the button out and relabel it "Scanning…",
    /// which said what was happening but left no way to call it off — and
    /// a silence pass over a long video is the longest thing this window
    /// does. Red, and it stops.
    /// </remarks>
    private void ShowScanning(bool scanning)
    {
        _scanning = scanning;
        ScanButton.Content = scanning ? "Stop" : "Scan";

        if (scanning)
        {
            ScanButton.Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x31, 0x2C));
            ScanButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8E, 0x26, 0x22));
            ScanButton.Foreground = Brushes.White;
        }
        else
        {
            // Back to the resource references the XAML set, not to fixed
            // brushes frozen out of the theme that was current a minute ago.
            ScanButton.SetResourceReference(BackgroundProperty, "PrimaryButton");
            ScanButton.SetResourceReference(BorderBrushProperty, "PrimaryButtonBorder");
            ScanButton.SetResourceReference(ForegroundProperty, "OnAccent");
        }
    }

    /// <summary>
    /// What the scan has to say for itself right now: how far through the
    /// video it has read, or moving dots before ffmpeg has said.
    /// </summary>
    /// <remarks>
    /// One step, so no step number: a detection is a single pass over the
    /// video, and "Step 1/1" would be a fraction with nothing to divide.
    /// The figures are a position in the video rather than a count of
    /// files, which is the honest measure of this particular job.
    /// </remarks>
    private void ShowWorking()
    {
        // Three dots down to none, then round again.
        var dots = new string('.', 3 - _dots % 4);
        _dots++;

        // ffmpeg words it as "Scanning 0:31 of 2:05", and the stage slot
        // beside it already says Scanning — so the verb comes off.
        const string said = "Scanning ";
        var figures = _position.StartsWith(said, StringComparison.Ordinal)
            ? _position[said.Length..]
            : _position;

        Say($"Scanning{dots}", figures, Elapsed);
    }

    /// <summary>
    /// Writes the status line: a stage in the fixed slot, the figures
    /// beside it, the clock against the right edge. A stage of its own
    /// takes the whole line, which is what the finished sentence and the
    /// stopped one want.
    /// </summary>
    private void Say(string stage, string info, string elapsed = "")
    {
        StageText.Width = stage.Length == 0 ? 0 : 158;
        StageText.Text = stage;
        InfoText.Text = info;
        ElapsedText.Text = elapsed;
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

    /// <summary>
    /// Stops a scan that is still going when the window is closed.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _running?.Cancel();
        _tick.Stop();
        base.OnClosed(e);
    }
}
