using System.IO;
using System.Reflection;
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

    /// <summary>A service whose constructor has not run, so it has no paths of its own.</summary>
    private static SettingsService Bare() =>
        (SettingsService)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(SettingsService));

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
