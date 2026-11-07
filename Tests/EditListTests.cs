using MpcHcVideoEditor.Services;
using MpcHcVideoEditor.Tests.Fixtures;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// Reading a file on the player's clock rather than the container's.
/// </summary>
/// <remarks>
/// An MP4 can carry an edit list saying which part of the media is the
/// presentation and where its clock starts. ffmpeg honours it, MPC-HC does
/// not, and this program sits between them. On a real file whose list began
/// 7.5 seconds in, every saved frame and every cut clip was seven and a half
/// seconds away from what was on screen.
///
/// The fix was one option on every input. These tests exist because it was
/// applied to eleven of fifteen call sites the first time, and the four it
/// missed included the one that cuts clips.
/// </remarks>
public class EditListTests
{
    private static FFmpegService Service() => new(TestVideos.FfmpegFolder);

    [Fact]
    public void The_fixture_really_does_carry_an_edit_list()
    {
        // Guard on the fixture itself: if ffmpeg ever stops writing an edit
        // list for a front-trimmed stream copy, every test below would pass
        // while testing nothing at all.
        var honoured = TestVideos.FrameTimes(TestVideos.WithEditList, ignoreEditList: false);
        var ignored = TestVideos.FrameTimes(TestVideos.WithEditList, ignoreEditList: true);

        Assert.True(ignored.Count > honoured.Count,
            "the fixture has no edit list, so these tests prove nothing");
    }

    [Fact]
    public async Task Duration_is_reported_on_the_players_clock()
    {
        // The player plays the media straight through, so the app has to
        // agree with that rather than with the container's trimmed view.
        var ignored = TestVideos.FrameTimes(TestVideos.WithEditList, ignoreEditList: true);
        var length = await Service().GetDurationAsync(TestVideos.WithEditList);

        Assert.True(length > ignored[^1] - 0.1,
            $"read {length:0.000}s, but the media runs to {ignored[^1]:0.000}s");
    }

    [Fact]
    public async Task A_frame_lookup_lands_on_the_media_timeline()
    {
        var video = TestVideos.WithEditList;
        var media = TestVideos.FrameTimes(video, ignoreEditList: true);

        var at = await Service().FrameStartAtAsync(video, 0.60);
        var expected = media.First(t => t >= 0.60 - 1e-6);

        Assert.Equal(expected, at, precision: 3);
    }

    [Fact]
    public async Task The_frame_saved_is_the_frame_the_player_would_show()
    {
        // The whole point, end to end. Without the fix this came back as a
        // frame from well later in the clip.
        var video = TestVideos.WithEditList;
        var service = Service();

        var at = await service.FrameStartAtAsync(video, 0.60);
        var png = await service.ExtractFrameAsync(video, at, height: 0);

        Assert.NotNull(png);

        // Against the same moment of the clip this was trimmed from, which
        // carries the same frames and no note about when to start showing
        // them. Honouring the edit list would land somewhere else entirely.
        var source = TestVideos.WithEditListSource;
        var reference = await service.ExtractFrameAsync(source, 0.60, height: 0);

        Assert.NotNull(reference);
        Assert.Equal(TestVideos.FrameIndexOf(source, reference!),
                     TestVideos.FrameIndexOf(source, png!));
    }
}
