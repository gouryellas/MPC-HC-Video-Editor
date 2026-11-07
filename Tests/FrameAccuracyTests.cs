using MpcHcVideoEditor.Services;
using MpcHcVideoEditor.Tests.Fixtures;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// Which frame comes back for a given moment — the question that took four
/// wrong answers to settle.
/// </summary>
/// <remarks>
/// Each test here is a bug that shipped. The rule arrived at is that the
/// displayed frame is the first to begin at or after the position the player
/// reports, because that clock lags the picture it is drawing and never leads
/// it. Both earlier rules are pinned as well, so neither can come back.
/// </remarks>
public class FrameAccuracyTests
{
    private static FFmpegService Service() => new(TestVideos.FfmpegFolder);

    [Fact]
    public async Task A_position_on_a_frame_boundary_takes_that_frame()
    {
        var video = TestVideos.ConstantRate;
        var at = await Service().FrameStartAtAsync(video, 0.40);

        Assert.Equal(0.40, at, precision: 3);
    }

    [Theory]
    // The clock lags the picture, so a position inside a frame belongs to the
    // frame that begins after it, not the one that began before.
    [InlineData(0.41, 0.44)]
    [InlineData(0.42, 0.44)]
    [InlineData(0.439, 0.44)]
    [InlineData(0.82, 0.84)]
    public async Task A_position_inside_a_frame_takes_the_next_one_to_begin(double position, double expected)
    {
        var at = await Service().FrameStartAtAsync(TestVideos.ConstantRate, position);

        Assert.Equal(expected, at, precision: 3);
    }

    [Fact]
    public async Task The_frame_rate_is_never_used_to_guess_where_frames_are()
    {
        // This clip claims 25fps and has nothing of the sort. A rule built on
        // the claimed rate put the answer a quarter of a second out; the only
        // way to be right is to read the times the file actually carries.
        var video = TestVideos.VariableRate;
        var times = TestVideos.FrameTimes(video);
        var service = Service();

        foreach (var position in new[] { 0.13, 0.25, 0.50, 0.70, 0.82 })
        {
            var expected = times.First(t => t >= position - 1e-6);
            var at = await service.FrameStartAtAsync(video, position);

            Assert.Equal(expected, at, precision: 3);
        }
    }

    [Fact]
    public async Task A_stream_that_does_not_start_at_zero_still_lines_up()
    {
        // The player counts from the start of playback; this file's timestamps
        // count from ten seconds. Comparing them directly found no frame at
        // all and fell back, silently, to the behaviour being replaced.
        var video = TestVideos.OffsetStart;
        var at = await Service().FrameStartAtAsync(video, 0.42);

        // Asked for 0.42 of the presentation, which is 10.42 of the stream.
        Assert.Equal(0.44, at, precision: 3);
    }

    [Fact]
    public async Task A_position_past_the_end_takes_the_last_frame_rather_than_failing()
    {
        var video = TestVideos.ConstantRate;
        var last = TestVideos.FrameTimes(video)[^1];

        var at = await Service().FrameStartAtAsync(video, 9999);

        Assert.Equal(last, at, precision: 3);
    }

    [Fact]
    public async Task The_frame_returned_is_the_frame_asked_for()
    {
        // End to end: the picture itself, not the timestamp. Every frame in
        // the fixture carries its own index as its brightness.
        var service = Service();
        var video = TestVideos.ConstantRate;

        foreach (var (position, index) in new[] { (0.40, 10), (0.41, 11), (0.82, 21), (1.00, 25) })
        {
            var at = await service.FrameStartAtAsync(video, position);
            var png = await service.ExtractFrameAsync(video, at, height: 0);

            Assert.NotNull(png);
            Assert.Equal(index, TestVideos.FrameIndexOf(video, png!));
        }
    }
}
