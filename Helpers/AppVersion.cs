using System.Reflection;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// The application's version, as it should be shown to a user.
/// </summary>
/// <remarks>
/// Read from the assembly rather than written out anywhere, so there is exactly
/// one place — <c>&lt;Version&gt;</c> in the .csproj — that decides it. The main
/// window's title used to carry a literal instead, and it drifted: 3.0.1
/// shipped with a window still captioned 3.0, because bumping the project file
/// had no reason to make anyone think of the XAML.
/// </remarks>
public static class AppVersion
{
    /// <summary>
    /// Short version string, e.g. <c>"3.0.2"</c>. Never empty — an assembly
    /// with no version at all reports "unknown" rather than leaving a caller
    /// to render a blank where a version should be.
    /// </summary>
    public static string Display { get; } = Read();

    private static string Read()
    {
        var asm = Assembly.GetExecutingAssembly();

        // InformationalVersion carries the full string including any suffix;
        // AssemblyVersion is the padded four-part form and reads oddly here.
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "unknown";

        // Strip the source-control hash MSBuild appends to
        // InformationalVersion — "3.0.2+9a1b2c3" reads as noise on screen.
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }
}
