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
    public bool DeleteToRecycleBin { get; private set; }
    public EncodingQuality Quality { get; private set; }
    public CollisionPolicy OnNameCollision { get; private set; }
    public PollSpeed PollSpeed { get; private set; }
    public int MpcWebInterfacePort { get; private set; }

    /// <summary>
    /// Whether to read the port out of MPC-HC's own settings rather than use
    /// <see cref="MpcWebInterfacePort"/>.
    /// </summary>
    public bool AutoDetectMpcWebInterface { get; private set; }
    public string FfmpegFolder { get; private set; }
    public bool ToastsEnabled { get; private set; }
    public double ToastSeconds { get; private set; }
    public bool RememberSaveToFolder { get; private set; }
    public RunMode RunMode { get; private set; }
    public bool AllowMultipleInstances { get; private set; }
    public OverlayCorner OverlayCorner { get; private set; }
    public double OverlayOpacity { get; private set; }
    public int MaxHistory { get; private set; }

    /// <summary>
    /// True when the user changed the ffmpeg folder, so the caller can say a
    /// restart is needed rather than leaving them wondering why nothing moved.
    /// </summary>
    public bool FfmpegFolderChanged { get; private set; }

    /// <summary>Chosen H.264 encoder.</summary>
    public VideoEncoder VideoEncoder { get; private set; }

    /// <summary>Whether cuts are re-encoded to land on the exact frame.</summary>
    public bool PreciseCuts { get; private set; }

    /// <summary>Whether written clips are brought to a common loudness.</summary>
    public bool NormaliseAudio { get; private set; }

    /// <summary>Pattern for output filenames.</summary>
    public string NameTemplate { get; private set; } = Helpers.NameTemplate.Default;

    /// <summary>Chosen colour theme.</summary>
    public string ThemeKey { get; private set; } = ThemePalette.Graphite.Key;

    /// <summary>
    /// The theme in force when the dialog opened, so Cancel can put it back.
    /// </summary>
    private readonly string _originalThemeKey;

    private readonly string _originalFfmpegFolder;

    /// <summary>
    /// Used to ask this machine which GPU encoders actually work. Optional so
    /// the dialog can still be constructed without one; the hardware options
    /// simply stay unavailable.
    /// </summary>
    private readonly FFmpegService? _ffmpeg;

    public SettingsDialog(AppSettings current, bool autoSwitchViews, FFmpegService? ffmpeg = null)
    {
        InitializeComponent();

        _ffmpeg = ffmpeg;

        // Seed every control from the live settings. Nothing is bound: the
        // dialog must not write through to the real object before Save.
        VideoFormatKey = VideoFormats.FromKey(current.DefaultVideoFormat).Key;
        AutoSwitchViews = autoSwitchViews;
        DeleteOriginalVideo = current.DeleteOriginalVideo;
        DeleteBookmarksFile = current.DeleteBookmarksFile;
        DeleteToRecycleBin = current.DeleteToRecycleBin;
        Quality = current.Quality;
        VideoEncoder = current.VideoEncoder;
        PreciseCuts = current.PreciseCuts;
        NormaliseAudio = current.NormaliseAudio;
        NameTemplate = string.IsNullOrWhiteSpace(current.NameTemplate)
            ? Helpers.NameTemplate.Default
            : current.NameTemplate;
        OnNameCollision = current.OnNameCollision;
        PollSpeed = current.PollSpeed;
        MpcWebInterfacePort = current.MpcWebInterfacePort;
        AutoDetectMpcWebInterface = current.AutoDetectMpcWebInterface;
        FfmpegFolder = current.FfmpegFolder ?? "";
        ToastsEnabled = current.ToastsEnabled;
        ToastSeconds = current.ToastSeconds;
        RememberSaveToFolder = current.RememberSaveToFolder;
        RunMode = current.RunMode;
        AllowMultipleInstances = current.AllowMultipleInstances;
        OverlayCorner = current.OverlayCorner;
        OverlayOpacity = current.OverlayOpacity;
        MaxHistory = current.MaxHistory;

        _originalFfmpegFolder = FfmpegFolder;

        ThemeKey = ThemePalette.FromKey(current.ThemeKey).Key;
        _originalThemeKey = ThemeKey;
        ThemeCombo.ItemsSource = ThemePalette.All;
        ThemeCombo.DisplayMemberPath = nameof(ThemePalette.Display);
        ThemeCombo.SelectedItem = ThemePalette.FromKey(ThemeKey);

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
        AutoDetectPortCheck.IsChecked = current.AutoDetectMpcWebInterface;
        RefreshDetectedPort();

        CutFast.IsChecked = !current.PreciseCuts;
        CutPrecise.IsChecked = current.PreciseCuts;
        NormaliseAudioCheck.IsChecked = current.NormaliseAudio;
        NameTemplateBox.Text = NameTemplate;
        VariableList.ItemsSource = Helpers.NameTemplate.Variables;
        TemplateExamples.ItemsSource = Helpers.NameTemplate.Examples;
        RefreshNameTemplatePreview();

        // The saved encoder is selected up front even if it turns out to be
        // unavailable — silently demoting someone's choice because a driver is
        // temporarily missing would lose the setting on the next Save.
        Pick(current.VideoEncoder);
        _ = ProbeEncodersAsync();
        FfmpegBox.Text = FfmpegFolder;

        Pick(current.DeleteOriginalVideo, VideoNever, VideoAsk, VideoAlways);
        Pick(current.DeleteBookmarksFile, CsvNever, CsvAsk, CsvAlways);

        RecycleBinCheck.IsChecked = current.DeleteToRecycleBin;
        RecycleBinCheck.Checked += (_, _) => ShowRecycleBinWarning();
        RecycleBinCheck.Unchecked += (_, _) => ShowRecycleBinWarning();
        ShowRecycleBinWarning();

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

        (current.AllowMultipleInstances ? InstanceMultiple : InstanceSingle).IsChecked = true;

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
    /// Shows the "permanently" warning only while the box is unchecked.
    /// </summary>
    /// <remarks>
    /// Kept out of sight in the safe state: a standing warning next to a
    /// setting that is behaving itself is noise, and noise is what stops the
    /// warning being read in the state that matters.
    /// </remarks>
    private void ShowRecycleBinWarning() =>
        RecycleBinWarning.Visibility = RecycleBinCheck.IsChecked == true
            ? Visibility.Collapsed
            : Visibility.Visible;

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
        DeleteToRecycleBin = RecycleBinCheck.IsChecked == true;

        Quality = QualityFast.IsChecked == true ? EncodingQuality.Fast
                : QualityHigh.IsChecked == true ? EncodingQuality.High
                : EncodingQuality.Balanced;

        VideoEncoder = EncoderNvenc.IsChecked == true ? VideoEncoder.Nvenc
                     : EncoderQsv.IsChecked == true ? VideoEncoder.QuickSync
                     : EncoderAmf.IsChecked == true ? VideoEncoder.Amf
                     : VideoEncoder.Software;

        PreciseCuts = CutPrecise.IsChecked == true;
        NormaliseAudio = NormaliseAudioCheck.IsChecked == true;

        // An empty box means the default, not an empty filename.
        var template = NameTemplateBox.Text?.Trim();
        NameTemplate = string.IsNullOrWhiteSpace(template) ? Helpers.NameTemplate.Default : template;

        OnNameCollision = CollisionIncrement.IsChecked == true ? CollisionPolicy.Increment
                        : CollisionOverwrite.IsChecked == true ? CollisionPolicy.Overwrite
                        : CollisionPolicy.Ask;

        PollSpeed = PollResponsive.IsChecked == true ? PollSpeed.Responsive
                  : PollLight.IsChecked == true ? PollSpeed.Light
                  : PollSpeed.Balanced;

        RunMode = ModeTray.IsChecked == true ? RunMode.SystemTray : RunMode.Application;

        AllowMultipleInstances = InstanceMultiple.IsChecked == true;

        OverlayCorner = CornerTopLeft.IsChecked == true ? OverlayCorner.TopLeft
                      : CornerBottomRight.IsChecked == true ? OverlayCorner.BottomRight
                      : CornerBottomLeft.IsChecked == true ? OverlayCorner.BottomLeft
                      : OverlayCorner.TopRight;

        MpcWebInterfacePort = port;
        AutoDetectMpcWebInterface = AutoDetectPortCheck.IsChecked == true;
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

    /// <summary>
    /// Shows what the template produces for a made-up clip.
    /// </summary>
    /// <remarks>
    /// Live, because a template is otherwise guesswork until a batch has
    /// already been written under the wrong name.
    /// </remarks>
    private void RefreshNameTemplatePreview()
    {
        if (NameTemplatePreview is null) return;
        NameTemplatePreview.Text = "Example:  " + Helpers.NameTemplate.Preview(NameTemplateBox.Text);
    }

    private void NameTemplate_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => RefreshNameTemplatePreview();

    /// <summary>
    /// Applies the chosen theme straight away, so it can be judged rather than
    /// guessed at from a name.
    /// </summary>
    private void Theme_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is not ThemePalette palette) return;
        ThemeKey = palette.Key;
        ThemeService.Apply(palette);
    }

    /// <summary>
    /// Puts the previous theme back when the dialog is dismissed without
    /// saving. Covers Escape and the close button as well as Cancel, which is
    /// why it hangs off the window closing rather than a button.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        if (DialogResult != true && ThemeService.Current.Key != _originalThemeKey)
            ThemeService.ApplyFromKey(_originalThemeKey);

        base.OnClosed(e);
    }

    /// <summary>
    /// Drops a variable into the box at the caret.
    /// </summary>
    /// <remarks>
    /// Typing <c>{number2}</c> by hand means getting the braces and the
    /// spelling right, and a typo now survives into the filename rather than
    /// being stripped. Clicking is both faster and impossible to misspell.
    /// </remarks>
    private void InsertVariable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string token }) return;

        var caret = NameTemplateBox.CaretIndex;
        var text = NameTemplateBox.Text ?? string.Empty;

        // Replace the selection when there is one, so a highlighted token can
        // be swapped for another in one click.
        var start = NameTemplateBox.SelectionLength > 0 ? NameTemplateBox.SelectionStart : caret;
        var length = NameTemplateBox.SelectionLength;

        NameTemplateBox.Text = text.Remove(start, length).Insert(start, token);
        NameTemplateBox.CaretIndex = start + token.Length;
        NameTemplateBox.Focus();
    }

    private void UseExample_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string template }) return;
        NameTemplateBox.Text = template;
        NameTemplateBox.CaretIndex = template.Length;
        NameTemplateBox.Focus();
    }

    private void ResetNameTemplate_Click(object sender, RoutedEventArgs e)
    {
        NameTemplateBox.Text = Helpers.NameTemplate.Default;
        RefreshNameTemplatePreview();
    }

    /// <summary>Selects the radio button for an encoder.</summary>
    private void Pick(VideoEncoder encoder)
    {
        EncoderSoftware.IsChecked = encoder == VideoEncoder.Software;
        EncoderNvenc.IsChecked    = encoder == VideoEncoder.Nvenc;
        EncoderQsv.IsChecked      = encoder == VideoEncoder.QuickSync;
        EncoderAmf.IsChecked      = encoder == VideoEncoder.Amf;
    }

    /// <summary>The radio button representing an encoder.</summary>
    private RadioButton ButtonFor(VideoEncoder encoder) => encoder switch
    {
        VideoEncoder.Nvenc     => EncoderNvenc,
        VideoEncoder.QuickSync => EncoderQsv,
        VideoEncoder.Amf       => EncoderAmf,
        _                      => EncoderSoftware
    };

    /// <summary>
    /// Asks this machine which GPU encoders actually run, and enables only
    /// those.
    /// </summary>
    /// <remarks>
    /// Each probe is a real encode of a fraction of a second, so this takes a
    /// moment per encoder and runs in the background rather than holding the
    /// dialog closed. The labels say "checking…" until an answer arrives, so a
    /// greyed-out option is never mistaken for a settled "no".
    /// </remarks>
    private async Task ProbeEncodersAsync()
    {
        foreach (var encoder in VideoEncoders.All)
        {
            if (encoder == VideoEncoder.Software) continue;

            var button = ButtonFor(encoder);
            var name = VideoEncoders.DisplayName(encoder);

            if (_ffmpeg is null)
            {
                button.Content = $"{name} — ffmpeg unavailable";
                continue;
            }

            bool ok;
            try { ok = await _ffmpeg.CanEncodeAsync(encoder); }
            catch { ok = false; }

            button.IsEnabled = ok;
            button.Content = ok ? name : $"{name} — not available on this machine";

            // A saved choice that no longer works must not stay selected, or
            // Save would write back an encoder that fails on the next job.
            if (!ok && button.IsChecked == true)
            {
                Pick(VideoEncoder.Software);
                button.Content = $"{name} — not available, switched to software";
            }
        }
    }

    /// <summary>
    /// Re-reads MPC-HC's settings and reflects them in the Player tab.
    /// </summary>
    /// <remarks>
    /// The manual port box stays enabled even while detection is on: it is
    /// still the fallback when nothing is found, so greying it out would hide
    /// the value that a portable or unusual install actually ends up using.
    /// </remarks>
    private void RefreshDetectedPort()
    {
        var auto = AutoDetectPortCheck.IsChecked == true;
        DetectedPortText.Text = auto
            ? MpcHcService.DescribeWebInterface()
            : "Detection is off — the port below is used as typed.";
    }

    private void AutoDetectPort_Changed(object sender, RoutedEventArgs e)
    {
        // Fires during InitializeComponent before the TextBlock exists.
        if (DetectedPortText is null) return;
        RefreshDetectedPort();
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
