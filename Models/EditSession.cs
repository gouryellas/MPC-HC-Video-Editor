using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MpcHcVideoEditor.Models;

public class EditSession : INotifyPropertyChanged
{
    private string _videoPath = string.Empty;
    private string _csvPath = string.Empty;
    private string _outputDirectory = string.Empty;
    private double _videoDurationSeconds;
    private double _currentTimeSeconds;

    public string VideoPath
    {
        get => _videoPath;
        set
        {
            _videoPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VideoFileName));
            OnPropertyChanged(nameof(HasVideo));
        }
    }

    public string CsvPath
    {
        get => _csvPath;
        set { _csvPath = value; OnPropertyChanged(); }
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set { _outputDirectory = value; OnPropertyChanged(); }
    }

    public double VideoDurationSeconds
    {
        get => _videoDurationSeconds;
        set { _videoDurationSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationDisplay)); }
    }

    public double CurrentTimeSeconds
    {
        get => _currentTimeSeconds;
        set { _currentTimeSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentTimeDisplay)); }
    }

    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    public string VideoFileName => string.IsNullOrEmpty(VideoPath)
        ? "<none>"
        : System.IO.Path.GetFileName(VideoPath);

    public bool HasVideo => !string.IsNullOrEmpty(VideoPath) && File.Exists(VideoPath);

    public string DurationDisplay => Bookmark.FormatTime(VideoDurationSeconds);
    public string CurrentTimeDisplay => Bookmark.FormatTime(CurrentTimeSeconds);

    public double SelectedDurationSeconds
    {
        get
        {
            var selected = Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
            if (selected.Count == 0)
                return Bookmarks.Where(b => b.IsValid).Sum(b => b.DurationSeconds / b.Speed);
            return selected.Sum(b => b.DurationSeconds / b.Speed);
        }
    }

    public string SelectedDurationDisplay => Bookmark.FormatDuration(SelectedDurationSeconds);

    public void NotifyDurationChanged() => OnPropertyChanged(nameof(SelectedDurationDisplay));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
