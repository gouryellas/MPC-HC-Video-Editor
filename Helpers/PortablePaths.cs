using System.IO;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Where the application keeps its data. Everything lives beside the
/// executable so the whole install is one folder that can be copied or moved
/// without leaving anything behind.
/// </summary>
public static class PortablePaths
{
    /// <summary>
    /// The folder holding the executable.
    /// </summary>
    /// <remarks>
    /// <see cref="Environment.ProcessPath"/> rather than
    /// <c>AppContext.BaseDirectory</c>: in a single-file build the latter can
    /// point at the temporary extraction folder, which is wiped between runs
    /// and would silently discard settings.
    /// </remarks>
    public static string AppFolder
    {
        get
        {
            var exe = Environment.ProcessPath;
            var dir = string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
            return string.IsNullOrEmpty(dir) ? AppContext.BaseDirectory : dir;
        }
    }

    /// <summary>A data folder beside the executable, created on demand.</summary>
    public static string DataFolder(string name)
    {
        var path = Path.Combine(AppFolder, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The pre-portable location under %APPDATA%, used once to carry existing
    /// settings over. Nothing is written here any more.
    /// </summary>
    public static string LegacyAppDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MPC-HC Video Editor");
}
