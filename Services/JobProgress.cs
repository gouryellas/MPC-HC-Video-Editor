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

    /// <summary>
    /// Bumped by every <see cref="Begin"/>. <see cref="CompleteAsync"/> captures
    /// it before its hold and checks it after, so a job started while the
    /// finished bar is still on screen is not hidden out from under itself.
    /// </summary>
    private int _generation;

    /// <summary>
    /// Raised once when a job completes successfully. Set by the view model,
    /// which owns the decision of what — if anything — that should do;
    /// <see cref="End"/> deliberately does not raise it, since a cancelled or
    /// failed job has nothing to announce.
    /// </summary>
    public Action? Finished;

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

    /// <summary>
    /// Action and current step on one line, e.g. "Merging files · Preparing".
    /// Joined rather than shown on separate rows because the step alone is
    /// rarely a sentence and the two together read as one.
    /// </summary>
    public string HeadlineDisplay =>
        string.IsNullOrWhiteSpace(Step) ? Action : $"{Action} · {Step}";

    /// <summary>"4/10" — blank for a single-file job.</summary>
    /// <remarks>
    /// <see cref="FileIndex"/> counts what is <em>finished</em>, not what is
    /// being started, so this always matches the numerator behind
    /// <see cref="Percent"/>. Counting the item in progress instead put the
    /// counter a whole step ahead of the bar: a five-file merge read "5/5" at
    /// 67% and then sat there for the join.
    /// </remarks>
    public string FileProgressDisplay => FileCount > 1 ? $"{FileIndex}/{FileCount}" : string.Empty;

    public string PercentDisplay => $"{Percent:0}%";

    /// <summary>Percent as 0–1, for the progress bar's horizontal scale.</summary>
    public double PercentFraction => Percent / 100.0;

    partial void OnActionChanged(string value) => OnPropertyChanged(nameof(HeadlineDisplay));
    partial void OnStepChanged(string value) => OnPropertyChanged(nameof(HeadlineDisplay));
    partial void OnFileIndexChanged(int value) => OnPropertyChanged(nameof(FileProgressDisplay));
    partial void OnFileCountChanged(int value) => OnPropertyChanged(nameof(FileProgressDisplay));
    partial void OnPercentChanged(double value)
    {
        OnPropertyChanged(nameof(PercentDisplay));
        OnPropertyChanged(nameof(PercentFraction));
        UpdateClock();
    }

    /// <summary>Starts a job and shows the panel.</summary>
    public void Begin(string action, int fileCount = 1)
    {
        _generation++;
        Action = action;
        FileCount = fileCount;

        // Nothing is finished yet. This used to open at 1, which read as "one
        // done" before any work had started and left every counter in the app
        // one step ahead of its own progress bar for the whole job.
        FileIndex = 0;
        CurrentFile = string.Empty;
        Step = string.Empty;
        Percent = 0;
        ElapsedDisplay = "0:00";
        RemainingDisplay = "—";
        _clock.Restart();
        IsRunning = true;
    }

    /// <summary>
    /// Names the file being worked on, and how many are finished behind it.
    /// </summary>
    /// <param name="finished">
    /// How many items are <em>complete</em> — not the position of the one
    /// starting. Callers in a loop pass the count done so far, so the counter
    /// and the percentage always tell the same story.
    /// </param>
    public void SetFile(int finished, string fileName)
    {
        FileIndex = finished;
        CurrentFile = fileName;
    }

    /// <summary>Reports the current step and overall percent.</summary>
    public void Report(string step, double percent)
    {
        Step = step;
        Percent = Math.Clamp(percent, 0, 100);
    }

    /// <summary>
    /// Finishes a job on a full green bar and holds the panel there before
    /// hiding it, so the outcome stays readable instead of vanishing the
    /// instant the last file is written.
    /// </summary>
    /// <param name="headline">What happened, e.g. "Merged 10 files into".</param>
    /// <param name="createdFile">The file produced, shown under the headline.</param>
    /// <param name="holdMs">How long the finished bar stays up.</param>
    public async Task CompleteAsync(string headline, string? createdFile = null, int holdMs = 3000)
    {
        var generation = _generation;

        // Every successful operation in the app ends here, which makes this the
        // one place a "finished" sound can be raised from without nine callers
        // having to remember it. Raised before the hold, not after: the sound is
        // for whoever is not watching the panel.
        Finished?.Invoke();

        _clock.Stop();
        if (FileCount > 0) FileIndex = FileCount;
        if (createdFile != null) CurrentFile = createdFile;
        Action = headline;
        Step = string.Empty;

        // Percent last of the three: setting it runs UpdateClock, which would
        // otherwise overwrite RemainingDisplay with "—" on a stopped clock.
        Percent = 100;
        RemainingDisplay = "0:00";

        await Task.Delay(holdMs);

        // Another job claimed the panel during the hold; it owns it now.
        if (_generation != generation) return;

        IsRunning = false;
        Percent = 0;
    }

    /// <summary>
    /// Fire-and-forget form of <see cref="CompleteAsync"/>, for callers with
    /// follow-up work — a cleanup prompt, an error dialog — that should not be
    /// made to sit through the hold. The generation check makes it safe to
    /// leave running unattended.
    /// </summary>
    public void Complete(string headline, string? createdFile = null, int holdMs = 3000)
        => _ = CompleteAsync(headline, createdFile, holdMs);

    /// <summary>
    /// Ends the job and hides the panel immediately. For cancellation and
    /// failure — successful jobs should finish through <see cref="CompleteAsync"/>.
    /// </summary>
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
