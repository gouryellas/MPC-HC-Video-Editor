using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Everything the program can say about its own state, in one place, ready to
/// be pasted into a bug report.
/// </summary>
/// <remarks>
/// Written after a frame-accuracy fault took most of a day. Every step of that
/// needed somebody to query MPC-HC's web interface by hand, run ffprobe
/// against the file, and compare the two answers — none of which the person
/// reporting it could produce, so the loop was "try this build", "still
/// wrong", repeated. Every number that mattered is below.
///
/// The questions it answers, in the order they turned out to matter:
/// what does the player think the time is, what does the file think it is,
/// do those two agree, and has anything been quietly failing.
/// </remarks>
public partial class DiagnosticsDialog : Window
{
    private readonly MpcHcService _mpc;
    private readonly FFmpegService _ffmpeg;
    private readonly string _videoPath;

    public DiagnosticsDialog(MpcHcService mpc, FFmpegService ffmpeg, string videoPath)
    {
        InitializeComponent();

        _mpc = mpc;
        _ffmpeg = ffmpeg;
        _videoPath = videoPath;

        Loaded += async (_, _) => await FillAsync();
    }

    private async Task FillAsync()
    {
        Report.Text = "Gathering…";

        // Awaited directly rather than pushed onto a worker: every step is an
        // external process this already waits on, so there is nothing to move
        // off the UI thread but the waiting itself.
        Report.Text = await BuildAsync();
    }

    private async Task<string> BuildAsync()
    {
        var sb = new StringBuilder();

        void Section(string name) => sb.AppendLine().AppendLine(name).AppendLine(new string('-', name.Length));
        void Line(string label, object? value) => sb.AppendLine($"  {label,-22} {value}");

        sb.AppendLine($"MPC-HC Video Editor {Version()}   {DateTime.Now:yyyy-MM-dd HH:mm}");

        // ---- the player ----
        Section("Player");

        try
        {
            var (position, duration, precise) = await _mpc.GetFreshPositionAsync();

            Line("position", $"{position:0.000}s");
            Line("duration", $"{duration:0.000}s");
            Line("source", precise
                ? "Web Interface (milliseconds)"
                : "window text (whole seconds — the frame can be a second out)");
            Line("port", _mpc.WebInterfacePort);
        }
        catch (Exception ex)
        {
            Line("could not be read", ex.Message);
        }

        // ---- the file, as ffmpeg sees it ----
        Section("Loaded video");

        if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
        {
            Line("file", "none loaded");
        }
        else
        {
            Line("file", Path.GetFileName(_videoPath));
            Line("size", $"{new FileInfo(_videoPath).Length / 1024 / 1024} MB");

            try
            {
                Line("duration (app)", $"{await _ffmpeg.GetDurationAsync(_videoPath):0.000}s");

                var (rateText, rate) = await _ffmpeg.GetFrameRateRationalAsync(_videoPath);
                Line("frame rate", $"{rateText} ({rate:0.###})");

                // The disagreement that caused the hardest bug in this
                // program's history. If these two differ, nothing downstream
                // can be trusted until that is explained.
                Line("edit list", await DescribeEditListAsync());
            }
            catch (Exception ex)
            {
                Line("could not be probed", ex.Message);
            }
        }

        // ---- the tools ----
        Section("Tools");
        Line("ffmpeg", Found("ffmpeg.exe"));
        Line("ffprobe", Found("ffprobe.exe"));

        // ---- what has gone wrong ----
        Section("Swallowed failures");

        if (!Diagnostics.HasEntries) sb.AppendLine("  none this session");
        else sb.Append(Indent(Diagnostics.Report()));

        return sb.ToString();
    }

    /// <summary>
    /// Whether the container trims or shifts the presentation, and by how
    /// much — stated as the two lengths, because the difference between them
    /// is the error everything else inherits.
    /// </summary>
    private async Task<string> DescribeEditListAsync()
    {
        var asWritten = await _ffmpeg.GetDurationAsync(_videoPath);
        var honoured = await ProbeDurationHonouringEditListAsync();

        if (honoured <= 0) return "could not be determined";

        var gap = Math.Abs(asWritten - honoured);

        return gap < 0.05
            ? "none — the file and the player agree"
            : $"YES — the player sees {asWritten:0.00}s, the container claims {honoured:0.00}s " +
              $"(a {gap:0.00}s shift, handled)";
    }

    private async Task<double> ProbeDurationHonouringEditListAsync()
    {
        try
        {
            var probe = Path.Combine(Path.GetDirectoryName(FindTool("ffprobe.exe")) ?? "", "ffprobe.exe");

            var psi = new System.Diagnostics.ProcessStartInfo(probe,
                $"-v error -show_entries format=duration -of csv=p=0 \"{_videoPath}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return 0;

            var text = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();

            return double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? d
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string Version() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";

    private static string Found(string tool)
    {
        var path = FindTool(tool);
        return path is null ? "NOT FOUND" : path;
    }

    private static string? FindTool(string tool)
    {
        foreach (var dir in new[]
                 {
                     PortablePaths.AppFolder,
                     Path.Combine(PortablePaths.AppFolder, "ffmpeg"),
                     AppContext.BaseDirectory
                 })
        {
            var candidate = Path.Combine(dir, tool);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static string Indent(string text) =>
        string.Join(Environment.NewLine,
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => "  " + l.TrimEnd()));

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await FillAsync();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Report.Text);
        }
        catch (Exception ex)
        {
            // The clipboard can be held by another process for a moment.
            Diagnostics.Swallowed("DiagnosticsDialog.Copy", ex);
        }
    }
}
