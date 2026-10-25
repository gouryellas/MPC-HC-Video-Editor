namespace MpcHcVideoEditor.Models;

/// <summary>
/// How the last thing said in the status bar went.
/// </summary>
/// <remarks>
/// Three values rather than a bool, because most of what the bar says is
/// neither. "Seeked to 1:04" and "Naming tag: none" are the ordinary traffic of
/// a session, and colouring them either way would make the colour mean nothing
/// by the time something actually succeeded or failed.
/// </remarks>
public enum StatusLevel
{
    /// <summary>Ordinary reporting. The strip keeps its theme colour.</summary>
    Normal,

    /// <summary>An operation finished and produced what it said it would.</summary>
    Ok,

    /// <summary>An operation failed.</summary>
    Failed
}
