using System.Windows;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>What Strip audio should leave on disk.</summary>
[Flags]
public enum StripAudioOutputs
{
    None = 0,

    /// <summary>The audio on its own, as an MP3.</summary>
    Audio = 1,

    /// <summary>The picture, with the audio track removed.</summary>
    SilentVideo = 2,

    Both = Audio | SilentVideo
}

/// <summary>
/// Asked once, before a Strip audio run, for the whole batch.
/// </summary>
/// <remarks>
/// The operation used to write an MP3 and nothing else, which is one of three
/// reasonable readings of its name — the other two being a silent video, and
/// both files at once. None of them is obviously the one meant, so it asks.
///
/// Asked once for the selection rather than per file: it is a statement of what
/// the run is for, and a batch of twenty files is not twenty separate
/// intentions.
/// </remarks>
public partial class StripAudioDialog : Window
{
    public StripAudioOutputs Outputs { get; private set; } = StripAudioOutputs.Audio;

    /// <param name="initial">
    /// The choice to preselect — the last one made this session, so a second
    /// run is one click. Not persisted: it is a per-run decision, and a setting
    /// remembered across launches would silently decide the next one.
    /// </param>
    /// <param name="videoExtension">
    /// The extension the silent copy would carry, so the option can name it
    /// rather than describing it.
    /// </param>
    public StripAudioDialog(StripAudioOutputs initial, string videoExtension)
    {
        InitializeComponent();

        AudioHint.Text = "An MP3 of the sound, at the highest quality the encoder offers.";
        VideoHint.Text = $"A copy of the picture as {videoExtension.TrimStart('.').ToUpperInvariant()}, "
                       + "with no sound, named with -silent on the end. Nothing is re-encoded, so it "
                       + "is quick and loses no quality.";

        switch (initial)
        {
            case StripAudioOutputs.SilentVideo: VideoOnly.IsChecked = true; break;
            case StripAudioOutputs.Both: Both.IsChecked = true; break;
            default: AudioOnly.IsChecked = true; break;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Outputs = VideoOnly.IsChecked == true ? StripAudioOutputs.SilentVideo
                : Both.IsChecked == true ? StripAudioOutputs.Both
                : StripAudioOutputs.Audio;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
