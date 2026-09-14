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
    /// A folder under %APPDATA% holding a spare copy of settings.json.
    /// </summary>
    /// <remarks>
    /// This is a backup, not a second home: the settings the application reads
    /// and writes still live beside the executable, and an install is still one
    /// folder that can be copied or moved. The copy here exists for one case —
    /// upgrading by deleting the program folder and unpacking a new one, which
    /// takes settings.json with it. When the application finds no settings
    /// beside the executable but does find this copy, it restores from it.
    ///
    /// It is also where settings lived before the application became portable,
    /// and the same path serves both purposes: a pre-portable install and a
    /// replaced folder look identical from here, and want the same answer.
    /// </remarks>
    public static string BackupFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MPC-HC Video Editor");
}
