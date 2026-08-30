using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
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

    /// <summary>The ranges the user accepted, in order.</summary>
    public List<DetectedRange> Accepted { get; private set; } = new();

    /// <summary>Whether the accepted ranges replace the current bookmarks.</summary>
    public bool ReplaceExisting { get; private set; }

    public DetectBookmarksDialog(FFmpegService ffmpeg, string videoPath)
    {
        InitializeComponent();
        _ffmpeg = ffmpeg;
        _videoPath = videoPath;
        ResultsList.ItemsSource = _proposals;
        ApplyDefaultsForMode();
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
        UseButton.IsEnabled = false;
        _proposals.Clear();
        ScanStatus.Text = "Scanning…";

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
                _proposals.Add(new Proposal
                {
                    Range = r,
                    Display = $"{n++}.   {Bookmark.FormatTime(r.Start)}  →  {Bookmark.FormatTime(r.End)}" +
                              $"   ({r.Duration:0.#}s)"
                });
            }

            ResultsHeading.Text = $"Proposed clips ({found.Count})";
            ScanStatus.Text = found.Count == 0
                ? "Nothing found — try a lower threshold or a shorter minimum clip."
                : $"Found {found.Count}. Untick anything you do not want.";
            UseButton.IsEnabled = found.Count > 0;
        }
        catch (Exception ex)
        {
            ScanStatus.Text = "Scan failed.";
            MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    private bool TryRead(System.Windows.Controls.TextBox box, out double value)
    {
        if (double.TryParse(box.Text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;

        MessageBox.Show(this, "That needs to be a number.", "Find clips",
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
            MessageBox.Show(this, "Nothing is ticked, so there is nothing to add.", "Find clips",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ReplaceExisting = ReplaceCheck.IsChecked == true;
        DialogResult = true;
    }
}
