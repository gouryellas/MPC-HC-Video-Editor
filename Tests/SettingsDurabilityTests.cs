using System.IO;
using System.Reflection;
using System.Text.Json;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// That a settings file survives being written badly.
/// </summary>
/// <remarks>
/// The failure these guard against lost everything and said nothing.
/// File.WriteAllText truncates before it writes, so an interruption left a
/// settings.json that existed and would not parse — and the spare copy was
/// only consulted when the file was missing, so a torn one was read as
/// "present but broken", replaced with defaults, and the spare overwritten
/// from those defaults by the next thing the user changed.
///
/// SettingsService fixes its own paths at construction, so the pieces are
/// exercised directly rather than through a constructed service.
/// </remarks>
public class SettingsDurabilityTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "mve-settings-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly MethodInfo WriteAtomic =
        typeof(SettingsService).GetMethod("WriteAtomic", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("WriteAtomic is gone — has the write been changed?");

    private static readonly MethodInfo Clamp =
        typeof(SettingsService).GetMethod("ClampNumericSettings", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("ClampNumericSettings is gone — has the guard been removed?");

    private static readonly MethodInfo TryLoadFrom =
        typeof(SettingsService).GetMethod("TryLoadFrom", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("TryLoadFrom is gone — has the load been changed?");

    public SettingsDurabilityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* a temp folder left behind is harmless */ }
    }

    private string Path_(string name) => Path.Combine(_dir, name);
    private static void Write(string path, string text) => WriteAtomic.Invoke(null, new object[] { path, text });

    /// <summary>
    /// A service whose constructor has not run, so it has no paths of its own
    /// — and therefore no settings either, since a field initialiser is part
    /// of the constructor. Given a fresh set here.
    /// </summary>
    private static SettingsService Bare()
    {
        var service = (SettingsService)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(SettingsService));

        typeof(SettingsService)
            .GetProperty(nameof(SettingsService.Current))!
            .SetValue(service, new AppSettings());

        return service;
    }

    private static bool Load(SettingsService service, string path) =>
        (bool)TryLoadFrom.Invoke(service, new object[] { path })!;

    [Fact]
    public void A_first_write_creates_the_file()
    {
        var f = Path_("settings.json");
        Write(f, "{\"A\":1}");

        Assert.Equal("{\"A\":1}", File.ReadAllText(f));
    }

    [Fact]
    public void A_later_write_replaces_it_whole()
    {
        var f = Path_("settings.json");
        Write(f, "{\"A\":1}");
        Write(f, "{\"A\":22222}");

        Assert.Equal("{\"A\":22222}", File.ReadAllText(f));
    }

    [Fact]
    public void Nothing_is_left_behind()
    {
        var f = Path_("settings.json");
        Write(f, "{\"A\":1}");

        Assert.False(File.Exists(f + ".tmp"));
    }

    [Fact]
    public void A_large_write_lands_whole()
    {
        var f = Path_("settings.json");
        var big = "{\"A\":\"" + new string('x', 2_000_000) + "\"}";

        Write(f, big);

        Assert.Equal(big.Length, File.ReadAllText(f).Length);
    }

    [Theory]
    [InlineData("{\"DefaultVideoFo")]   // torn mid-write
    [InlineData("")]                    // truncated to nothing
    [InlineData("not json at all")]
    public void An_unreadable_file_is_refused_rather_than_taken_as_settings(string contents)
    {
        // This is the one that used to lose everything: a file in this state
        // was "present", so the spare was never consulted.
        var f = Path_("broken.json");
        File.WriteAllText(f, contents);

        Assert.False(Load(Bare(), f));
    }

    [Fact]
    public void A_missing_file_is_refused()
    {
        Assert.False(Load(Bare(), Path_("nothing-here.json")));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_number_that_is_not_a_number_cannot_reach_the_serializer(double poison)
    {
        // The failure this guards against lost a whole session's settings and
        // said nothing until the save started reporting. A window that has
        // never been shown reports its position as NaN; that reached the
        // settings object, and JSON cannot write a NaN — so the one bad number
        // meant the entire file failed to save, every time, for the rest of
        // the run.
        //
        // Math.Clamp is no defence against the one that actually happened.
        // An infinity it does clamp — infinity is greater than the maximum,
        // so the comparison works. NaN it hands straight back, because every
        // comparison against NaN is false and so neither bound is breached.
        if (double.IsNaN(poison)) Assert.Equal(poison, Math.Clamp(poison, 0.0, 1.0));

        var service = Bare();
        service.Current.FadeSeconds = poison;
        service.Current.ToastSeconds = poison;
        service.Current.OverlayOpacity = poison;
        service.Current.SeekbarOpacity = poison;
        service.Current.MainWindowPlacement = new WindowPlacement(poison, poison, poison, poison, false);

        Clamp.Invoke(service, null);

        // Every one replaced, and the placement dropped outright — there is no
        // sensible place to move a window to on its behalf.
        Assert.True(double.IsFinite(service.Current.FadeSeconds));
        Assert.True(double.IsFinite(service.Current.ToastSeconds));
        Assert.True(double.IsFinite(service.Current.OverlayOpacity));
        Assert.True(double.IsFinite(service.Current.SeekbarOpacity));
        Assert.Null(service.Current.MainWindowPlacement);

        // And the whole object now survives the journey it could not make.
        var json = JsonSerializer.Serialize(service.Current);
        Assert.DoesNotContain("NaN", json);
        Assert.DoesNotContain("Infinity", json);
    }

    [Fact]
    public void A_real_window_position_is_left_alone()
    {
        var service = Bare();
        service.Current.MainWindowPlacement = new WindowPlacement(100, 200, 1100, 780, false);

        Clamp.Invoke(service, null);

        Assert.NotNull(service.Current.MainWindowPlacement);
        Assert.Equal(1100, service.Current.MainWindowPlacement!.Width);
    }

    [Fact]
    public void Numbers_outside_their_range_are_still_brought_back_in()
    {
        var service = Bare();
        service.Current.ToastSeconds = 9999;
        service.Current.SeekbarOpacity = -5;

        Clamp.Invoke(service, null);

        Assert.Equal(10.0, service.Current.ToastSeconds);
        Assert.Equal(0.1, service.Current.SeekbarOpacity);
    }

    [Fact]
    public void A_good_file_loads_and_its_values_arrive()
    {
        var f = Path_("good.json");
        File.WriteAllText(f, "{\"DefaultVideoFormat\":\"mkv\",\"MaxHistory\":7}");

        var service = Bare();

        Assert.True(Load(service, f));
        Assert.Equal("mkv", service.Current.DefaultVideoFormat);
        Assert.Equal(7, service.Current.MaxHistory);
    }
}
