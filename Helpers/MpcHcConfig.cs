using System.IO;
using Microsoft.Win32;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// What the player's own configuration says about its Web Interface.
/// </summary>
/// <param name="Enabled">
/// Whether the player is actually serving. A port is worthless if this is off,
/// and "off" is by far the most common reason seeking never works.
/// </param>
/// <param name="Port">The port the player is configured to listen on.</param>
/// <param name="LocalhostOnly">
/// Whether the player restricts the interface to loopback. This application
/// only ever connects to 127.0.0.1, so the restriction costs it nothing —
/// worth reading only so the setting can be reported accurately.
/// </param>
/// <param name="Source">Where the answer came from, for showing the user.</param>
public sealed record MpcHcWebConfig(bool Enabled, int Port, bool LocalhostOnly, string Source);

/// <summary>
/// Reads MPC-HC's (or MPC-BE's) own settings, so the Web Interface port does
/// not have to be kept in step by hand.
/// </summary>
/// <remarks>
/// The port was a setting the user had to match against a number buried in the
/// player's options, with a silent fallback to slower seek-bar clicking when
/// they did not. The player already knows the answer; this asks it.
/// </remarks>
public static class MpcHcConfig
{
    /// <summary>
    /// Where the installed players keep their settings, most current first.
    /// Absent keys are simply a miss — a machine with only one of these
    /// installed is the normal case, not an error.
    /// </summary>
    private static readonly string[] RegistryPaths =
    {
        @"Software\MPC-HC\MPC-HC\Settings",
        @"Software\MPC-BE\Settings",
        @"Software\Gabest\Media Player Classic\Settings"
    };

    /// <summary>
    /// Reads the Web Interface configuration, or returns <c>null</c> when the
    /// player's settings cannot be found at all.
    /// </summary>
    /// <param name="playerExePath">
    /// The player executable, when known. A portable install keeps its
    /// settings in an .ini beside the executable and never touches the
    /// registry, so that has to be looked at first — otherwise a stale
    /// registry key left by an earlier installed copy wins over the settings
    /// actually in use.
    /// </param>
    public static MpcHcWebConfig? Detect(string? playerExePath = null)
        => FromPortableIni(playerExePath) ?? FromRegistry();

    private static MpcHcWebConfig? FromPortableIni(string? playerExePath)
    {
        if (string.IsNullOrWhiteSpace(playerExePath)) return null;

        try
        {
            var dir = Path.GetDirectoryName(playerExePath);
            if (string.IsNullOrEmpty(dir)) return null;

            // Portable builds write "<exe name>.ini" next to themselves; the
            // rest are listed so an oddly-renamed executable still resolves.
            var candidates = new[]
            {
                Path.Combine(dir, Path.GetFileNameWithoutExtension(playerExePath) + ".ini"),
                Path.Combine(dir, "mpc-hc64.ini"),
                Path.Combine(dir, "mpc-hc.ini"),
                Path.Combine(dir, "mpc-be64.ini"),
                Path.Combine(dir, "mpc-be.ini")
            }.Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var ini in candidates)
            {
                if (!File.Exists(ini)) continue;

                var values = ReadIniSection(ini, "Settings");
                if (!values.TryGetValue("WebServerPort", out var portText)) continue;
                if (!int.TryParse(portText, out var port)) continue;

                return new MpcHcWebConfig(
                    Enabled: values.GetValueOrDefault("EnableWebServer") == "1",
                    Port: port,
                    LocalhostOnly: values.GetValueOrDefault("WebServerLocalhostOnly", "1") == "1",
                    Source: Path.GetFileName(ini));
            }
        }
        catch
        {
            // An unreadable ini is a miss, not a failure worth surfacing.
        }

        return null;
    }

    private static MpcHcWebConfig? FromRegistry()
    {
        foreach (var path in RegistryPaths)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                if (key?.GetValue("WebServerPort") is not int port) continue;

                // These are DWORDs. A missing EnableWebServer means the player
                // has never been told to serve, so the honest default is off;
                // a missing LocalhostOnly matches the player's own default of
                // on.
                var enabled = key.GetValue("EnableWebServer") as int? ?? 0;
                var localOnly = key.GetValue("WebServerLocalhostOnly") as int? ?? 1;

                return new MpcHcWebConfig(enabled != 0, port, localOnly != 0, @"HKCU\" + path);
            }
            catch
            {
                // Denied or malformed key — try the next candidate.
            }
        }

        return null;
    }

    /// <summary>
    /// Pulls one section out of an INI file.
    /// </summary>
    /// <remarks>
    /// <see cref="File.ReadAllLines(string)"/> detects byte-order marks, which
    /// matters: the player writes this file as UTF-16 in some builds and UTF-8
    /// in others, and reading one as the other yields nothing usable.
    /// </remarks>
    private static Dictionary<string, string> ReadIniSection(string path, string section)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inSection = false;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                inSection = string.Equals(line[1..^1].Trim(), section, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            result[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }

        return result;
    }
}
