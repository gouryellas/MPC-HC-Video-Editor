using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Tracks a multi-file operation for the progress panel: what is running,
/// which file of how many, what step, percent complete, and elapsed/remaining
/// time. One instance is reused for every job.
/// </summary>
public partial class JobProgress : ObservableObject
{
    private readonly Stopwatch _clock = new();

    /// <summary>True while a job is running — drives the panel's visibility.</summary>
    [ObservableProperty] private bool _isRunning;

    /// <summary>The operation, e.g. "Convert video".</summary>
    [ObservableProperty] private string _action = string.Empty;

    /// <summary>The file being worked on right now.</summary>
    [ObservableProperty] private string _currentFile = string.Empty;

    /// <summary>What is happening to it, e.g. "Encoding segment 2/3".</summary>
    [ObservableProperty] private string _step = string.Empty;

    [ObservableProperty] private int _fileIndex;
    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private double _percent;

    [ObservableProperty] private string _elapsedDisplay = "0:00";
    [ObservableProperty] private string _remainingDisplay = "—";

    /// <summary>"File 2 of 5" — blank for a single-file job.</summary>
    public string FileCountDisplay => FileCount > 1 ? $"File {FileIndex} of {FileCount}" : string.Empty;

    public string PercentDisplay => $"{Percent:0}%";

    /// <summary>Percent as 0–1, for the progress bar's horizontal scale.</summary>
    public double PercentFraction => Percent / 100.0;

    partial void OnFileIndexChanged(int value) => OnPropertyChanged(nameof(FileCountDisplay));
    partial void OnFileCountChanged(int value) => OnPropertyChanged(nameof(FileCountDisplay));
    partial void OnPercentChanged(double value)
    {
        OnPropertyChanged(nameof(PercentDisplay));
        OnPropertyChanged(nameof(PercentFraction));
        UpdateClock();
    }

    /// <summary>Starts a job and shows the panel.</summary>
    public void Begin(string action, int fileCount = 1)
    {
        Action = action;
        FileCount = fileCount;
        FileIndex = fileCount > 0 ? 1 : 0;
        CurrentFile = string.Empty;
        Step = string.Empty;
        Percent = 0;
        ElapsedDisplay = "0:00";
        RemainingDisplay = "—";
        _clock.Restart();
        IsRunning = true;
    }

    /// <summary>Moves to a file within the job.</summary>
    public void SetFile(int index, string fileName)
    {
        FileIndex = index;
        CurrentFile = fileName;
    }

    /// <summary>Reports the current step and overall percent.</summary>
    public void Report(string step, double percent)
    {
        Step = step;
        Percent = Math.Clamp(percent, 0, 100);
    }

    /// <summary>Ends the job and hides the panel.</summary>
    public void End(string? finalStep = null)
    {
        _clock.Stop();
        if (finalStep != null) Step = finalStep;
        Percent = 0;
        IsRunning = false;
    }

    /// <summary>
    /// Refreshes elapsed and projects remaining from the rate so far. The
    /// estimate is deliberately withheld below 3% — early on it is dominated
    /// by startup cost and swings wildly enough to be worse than nothing.
    /// </summary>
    private void UpdateClock()
    {
        var elapsed = _clock.Elapsed;
        ElapsedDisplay = Format(elapsed);

        if (Percent < 3 || !_clock.IsRunning)
        {
            RemainingDisplay = "—";
            return;
        }

        var total = elapsed.TotalSeconds * 100.0 / Percent;
        var left = TimeSpan.FromSeconds(Math.Max(0, total - elapsed.TotalSeconds));
        RemainingDisplay = Format(left);
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
}
