using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// The File ▸ Settings window.
/// </summary>
/// <remarks>
/// Edits a copy and only writes back on Save, so Cancel really does cancel —
/// including the view-switching toggle, which has a visible side effect the
/// moment it is applied. The caller reads the properties below and pushes them
/// into <see cref="SettingsService"/>; the dialog itself never saves, which
/// keeps persistence and the ViewModel's own reaction to a changed setting in
/// one place.
///
/// Numeric fields are validated on Save rather than while typing. Rejecting
/// keystrokes makes a box that cannot be cleared and retyped, and every value
/// here is also clamped by <see cref="SettingsService"/> on the way to disk —
/// so the check is about telling the user, not about protecting the file.
/// </remarks>
public partial class SettingsDialog : Window
{
    /// <summary>Chosen container, as a <see cref="VideoFormats.Format.Key"/>.</summary>
    public string VideoFormatKey { get; private set; }

    public bool AutoSwitchViews { get; private set; }
    public CleanupMode DeleteOriginalVideo { get; private set; }
    public CleanupMode DeleteBookmarksFile { get; private set; }
    public EncodingQuality Quality { get; private set; }
    public CollisionPolicy OnNameCollision { get; private set; }
    public PollSpeed PollSpeed { get; private set; }
    public int MpcWebInterfacePort { get; private set; }
    public string FfmpegFolder { get; private set; }
    public bool ToastsEnabled { get; private set; }
    public double ToastSeconds { get; private set; }
    public bool RememberSaveToFolder { get; private set; }
    public RunMode RunMode { get; private set; }
    public OverlayCorner OverlayCorner { get; private set; }
    public double OverlayOpacity { get; private set; }
    public int MaxHistory { get; private set; }

    /// <summary>
    /// True when the user changed the ffmpeg folder, so the caller can say a
    /// restart is needed rather than leaving them wondering why nothing moved.
    /// </summary>
    public bool FfmpegFolderChanged { get; private set; }

    private readonly string _originalFfmpegFolder;

    public SettingsDialog(AppSettings current, bool autoSwitchViews)
    {
        InitializeComponent();

        // Seed every control from the live settings. Nothing is bound: the
        // dialog must not write through to the real object before Save.
        VideoFormatKey = VideoFormats.FromKey(current.DefaultVideoFormat).Key;
        AutoSwitchViews = autoSwitchViews;
        DeleteOriginalVideo = current.DeleteOriginalVideo;
        DeleteBookmarksFile = current.DeleteBookmarksFile;
        Quality = current.Quality;
        OnNameCollision = current.OnNameCollision;
        PollSpeed = current.PollSpeed;
        MpcWebInterfacePort = current.MpcWebInterfacePort;
        FfmpegFolder = current.FfmpegFolder ?? "";
        ToastsEnabled = current.ToastsEnabled;
        ToastSeconds = current.ToastSeconds;
        RememberSaveToFolder = current.RememberSaveToFolder;
        RunMode = current.RunMode;
        OverlayCorner = current.OverlayCorner;
        OverlayOpacity = current.OverlayOpacity;
        MaxHistory = current.MaxHistory;

        _originalFfmpegFolder = FfmpegFolder;

        // Bind the format list by object so the selection round-trips as a
        // Format rather than a display string that would have to be parsed
        // back. DisplayMemberPath keeps the codec note visible in the list.
        FormatCombo.ItemsSource = VideoFormats.All;
        FormatCombo.DisplayMemberPath = nameof(VideoFormats.Format.Display);
        FormatCombo.SelectedItem = VideoFormats.FromKey(current.DefaultVideoFormat);

        AutoSwitchCheck.IsChecked = autoSwitchViews;
        ToastsCheck.IsChecked = current.ToastsEnabled;
        RememberSaveToCheck.IsChecked = current.RememberSaveToFolder;

        ToastSecondsBox.Text = current.ToastSeconds.ToString("0.#", CultureInfo.CurrentCulture);
        MaxHistoryBox.Text = current.MaxHistory.ToString(CultureInfo.CurrentCulture);
        PortBox.Text = current.MpcWebInterfacePort.ToString(CultureInfo.CurrentCulture);
        FfmpegBox.Text = FfmpegFolder;

        Pick(current.DeleteOriginalVideo, VideoNever, VideoAsk, VideoAlways);
        Pick(current.DeleteBookmarksFile, CsvNever, CsvAsk, CsvAlways);

        (current.Quality switch
        {
            EncodingQuality.Fast => QualityFast,
            EncodingQuality.High => QualityHigh,
            _ => QualityBalanced
        }).IsChecked = true;

        (current.OnNameCollision switch
        {
            CollisionPolicy.Increment => CollisionIncrement,
            CollisionPolicy.Overwrite => CollisionOverwrite,
            _ => CollisionAsk
        }).IsChecked = true;

        (current.PollSpeed switch
        {
            PollSpeed.Responsive => PollResponsive,
            PollSpeed.Light => PollLight,
            _ => PollBalanced
        }).IsChecked = true;

        (current.RunMode == RunMode.SystemTray ? ModeTray : ModeApplication).IsChecked = true;

        (current.OverlayCorner switch
        {
            OverlayCorner.TopLeft => CornerTopLeft,
            OverlayCorner.BottomRight => CornerBottomRight,
            OverlayCorner.BottomLeft => CornerBottomLeft,
            _ => CornerTopRight
        }).IsChecked = true;

        OpacitySlider.Value = Math.Clamp(current.OverlayOpacity, 0.3, 1.0);
        OpacitySlider.ValueChanged += (_, _) => ShowOpacity();
        ShowOpacity();

        ShowFfmpegStatus();
        FfmpegBox.TextChanged += (_, _) => ShowFfmpegStatus();
    }

    private void ShowOpacity() =>
        OpacityValue.Text = $"{OpacitySlider.Value * 100:0}%";

    /// <summary>
    /// Says whether the folder in the box actually holds ffmpeg, so a typo is
    /// visible here rather than at the first failed operation.
    /// </summary>
    private void ShowFfmpegStatus()
    {
        var folder = FfmpegBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(folder))
        {
            FfmpegStatus.Text = "";
            return;
        }

        try
        {
            var hasFfmpeg = File.Exists(Path.Combine(folder, "ffmpeg.exe"));
            var hasFfprobe = File.Exists(Path.Combine(folder, "ffprobe.exe"));

            FfmpegStatus.Text = (hasFfmpeg, hasFfprobe) switch
            {
                (true, true) => "✓ ffmpeg.exe and ffprobe.exe found",
                (true, false) => "⚠ ffmpeg.exe found, ffprobe.exe missing — durations will read as zero",
                (false, true) => "⚠ ffprobe.exe found, ffmpeg.exe missing — nothing will encode",
                _ => "⚠ neither ffmpeg.exe nor ffprobe.exe is in this folder"
            };
        }
        catch
        {
            // An unreadable path is the user's to fix; do not editorialise.
            FfmpegStatus.Text = "⚠ this folder could not be read";
        }
    }

    private static void Pick(CleanupMode mode, RadioButton never, RadioButton ask, RadioButton always)
    {
        // An unrecognised value lands on Keep — the option that cannot cost
        // the user a file.
        var chosen = mode switch
        {
            CleanupMode.Ask => ask,
            CleanupMode.Always => always,
            _ => never
        };
        chosen.IsChecked = true;
    }

    private static CleanupMode ReadCleanup(RadioButton ask, RadioButton always) =>
        always.IsChecked == true ? CleanupMode.Always
        : ask.IsChecked == true ? CleanupMode.Ask
        : CleanupMode.Never;

    private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select the folder containing ffmpeg.exe" };

        var current = FfmpegBox.Text?.Trim();
        if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
            dlg.InitialDirectory = current;

        if (dlg.ShowDialog() == true)
            FfmpegBox.Text = dlg.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate the typed fields before committing anything, so a rejected
        // value leaves the whole dialog as it was rather than half-applied.
        if (!TryReadInt(PortBox, "web interface port", 1, 65535, out var port)) return;
        if (!TryReadInt(MaxHistoryBox, "recent videos count", 1, 50, out var history)) return;
        if (!TryReadDouble(ToastSecondsBox, "toast duration", 0.5, 10.0, out var toastSeconds)) return;

        VideoFormatKey = (FormatCombo.SelectedItem as VideoFormats.Format)?.Key
                         ?? VideoFormats.Default.Key;

        AutoSwitchViews = AutoSwitchCheck.IsChecked == true;
        ToastsEnabled = ToastsCheck.IsChecked == true;
        RememberSaveToFolder = RememberSaveToCheck.IsChecked == true;

        DeleteOriginalVideo = ReadCleanup(VideoAsk, VideoAlways);
        DeleteBookmarksFile = ReadCleanup(CsvAsk, CsvAlways);

        Quality = QualityFast.IsChecked == true ? EncodingQuality.Fast
                : QualityHigh.IsChecked == true ? EncodingQuality.High
                : EncodingQuality.Balanced;

        OnNameCollision = CollisionIncrement.IsChecked == true ? CollisionPolicy.Increment
                        : CollisionOverwrite.IsChecked == true ? CollisionPolicy.Overwrite
                        : CollisionPolicy.Ask;

        PollSpeed = PollResponsive.IsChecked == true ? PollSpeed.Responsive
                  : PollLight.IsChecked == true ? PollSpeed.Light
                  : PollSpeed.Balanced;

        RunMode = ModeTray.IsChecked == true ? RunMode.SystemTray : RunMode.Application;

        OverlayCorner = CornerTopLeft.IsChecked == true ? OverlayCorner.TopLeft
                      : CornerBottomRight.IsChecked == true ? OverlayCorner.BottomRight
                      : CornerBottomLeft.IsChecked == true ? OverlayCorner.BottomLeft
                      : OverlayCorner.TopRight;

        MpcWebInterfacePort = port;
        MaxHistory = history;
        ToastSeconds = toastSeconds;
        OverlayOpacity = OpacitySlider.Value;

        FfmpegFolder = FfmpegBox.Text?.Trim() ?? "";
        FfmpegFolderChanged = !string.Equals(FfmpegFolder, _originalFfmpegFolder,
                                             StringComparison.OrdinalIgnoreCase);

        // Setting DialogResult closes a modal window on its own; no Close()
        // call, which would be a second close.
        DialogResult = true;
    }

    private bool TryReadInt(TextBox box, string what, int min, int max, out int value)
    {
        if (int.TryParse(box.Text?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            && value >= min && value <= max)
            return true;

        Complain(box, what, $"{min} and {max}");
        return false;
    }

    private bool TryReadDouble(TextBox box, string what, double min, double max, out double value)
    {
        if (double.TryParse(box.Text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && value >= min && value <= max)
            return true;

        Complain(box, what, $"{min} and {max}");
        return false;
    }

    private void Complain(TextBox box, string what, string range)
    {
        MessageBox.Show(this,
            $"The {what} must be a number between {range}.",
            "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);

        box.Focus();
        box.SelectAll();
    }
}
