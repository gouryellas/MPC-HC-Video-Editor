namespace MpcHcVideoEditor.Models;

/// <summary>
/// Where a window was and how big, for putting it back next time.
/// </summary>
/// <remarks>
/// The size is the restored size even when <see cref="Maximized"/> is set.
/// Storing the maximized footprint instead would mean a window un-maximized
/// after a restart filled the screen anyway, having forgotten the size it was
/// before — which is what <see cref="System.Windows.Window.RestoreBounds"/>
/// exists to answer.
/// </remarks>
public sealed record WindowPlacement(double Left, double Top,
                                     double Width, double Height,
                                     bool Maximized);
