using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Deletes the files this app removes on the user's behalf, waiting for
/// whatever is still holding them to let go. Sends them to the Windows Recycle
/// Bin unless <see cref="SendToBin"/> has been turned off.
/// </summary>
/// <remarks>
/// <see cref="File.Delete"/> is unrecoverable, which is the wrong default for
/// anything this app removes on the user's behalf — especially the settings
/// that delete without asking first. The bin is what makes those settings
/// defensible: the worst case becomes a trip to the bin rather than lost
/// footage. It is therefore the default, and turning it off is a deliberate
/// choice made in Settings.
///
/// Uses <see cref="FileSystem.DeleteFile(string, UIOption, RecycleOption)"/>
/// rather than a hand-rolled <c>SHFileOperation</c> P/Invoke. It is the same
/// shell operation underneath, but the struct marshalling — whose packing is
/// a well-known source of x64 corruption when declared by hand — is already
/// correct. Microsoft.VisualBasic.Core ships with the .NET runtime, so this
/// costs no extra package reference.
/// </remarks>
public static class RecycleBin
{
    /// <summary>
    /// Whether deletions go to the Recycle Bin. True unless the user has turned
    /// it off; see <c>AppSettings.DeleteToRecycleBin</c>.
    /// </summary>
    /// <remarks>
    /// Static, and pushed in by <c>MainViewModel.ApplyServiceSettings</c>
    /// alongside every other setting a service needs, because the callers are
    /// scattered across the ViewModel and threading the flag through each one
    /// would put a delete-policy parameter on methods that have no business
    /// deciding it.
    ///
    /// Defaults to true so that any path which runs before settings are applied
    /// — or a caller that forgets — still lands on the recoverable behaviour.
    /// </remarks>
    public static bool SendToBin { get; set; } = true;

    /// <summary>
    /// How long the non-blocking path keeps trying. Generous, because the
    /// thing being waited on is usually an ffmpeg that has just exited, and
    /// the file is one the user has explicitly asked to be rid of.
    /// </summary>
    private static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the blocking path keeps trying. Deliberately much shorter:
    /// its callers hold the UI thread, and they delete small files — a
    /// bookmark CSV, a converted image — that nothing else has open for long.
    /// </summary>
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(1);

    private const int PollMs = 200;

    /// <summary>
    /// True when the file cannot currently be opened exclusively, i.e. some
    /// other handle is still on it.
    /// </summary>
    /// <remarks>
    /// Only <see cref="IOException"/> counts as "in use". An
    /// <see cref="UnauthorizedAccessException"/> means read-only or a
    /// permissions problem, which no amount of waiting will change — treating
    /// that as a lock would spin for the whole timeout and then fail anyway.
    /// </remarks>
    private static bool IsInUse(string path)
    {
        try
        {
            using var _ = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            // Not a lock. Let the delete attempt produce the real error.
            return false;
        }
    }

    /// <summary>
    /// Deletes one file, retrying while it is still in use — to the Recycle Bin
    /// unless <see cref="SendToBin"/> is off. Blocks the calling thread; prefer
    /// <see cref="TryDeleteAsync"/> from the UI thread.
    /// </summary>
    public static bool TryDelete(string path, out string? error) =>
        TryDelete(path, SyncTimeout, out error);

    /// <inheritdoc cref="TryDelete(string, out string?)"/>
    public static bool TryDelete(string path, TimeSpan timeout, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return true;

        var deadline = DateTime.UtcNow + timeout;

        while (IsInUse(path) && DateTime.UtcNow < deadline)
            Thread.Sleep(PollMs);

        return Delete(path, deadline, sleep: ms => Thread.Sleep(ms), out error);
    }

    /// <summary>
    /// Deletes one file — to the Recycle Bin unless <see cref="SendToBin"/> is
    /// off — waiting without blocking for whatever still holds it to release it.
    /// </summary>
    /// <remarks>
    /// The wait exists because cleanup runs the instant an operation reports
    /// success, and "success" is the ffmpeg process exiting — which happens a
    /// moment before Windows releases its handle on the source file. Deleting
    /// then failed with a sharing violation on a file that was, half a second
    /// later, perfectly deletable.
    /// </remarks>
    public static async Task<(bool Ok, string? Error)> TryDeleteAsync(string path, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return (true, null);

        var deadline = DateTime.UtcNow + (timeout ?? AsyncTimeout);

        while (IsInUse(path) && DateTime.UtcNow < deadline)
            await Task.Delay(PollMs);

        var ok = Delete(path, deadline, sleep: null, out var error);
        if (ok) return (true, null);

        // One last go after a beat: the exclusive-open probe can succeed while
        // the shell operation still trips over a lingering handle.
        if (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollMs);
            ok = Delete(path, DateTime.UtcNow, sleep: null, out error);
        }

        return (ok, error);
    }

    /// <summary>
    /// The delete itself, retried until <paramref name="deadline"/>.
    /// </summary>
    /// <param name="sleep">
    /// How to pause between attempts, or null to make a single attempt. The
    /// async caller does its own waiting rather than blocking a thread here.
    /// </param>
    private static bool Delete(string path, DateTime deadline, Action<int>? sleep, out string? error)
    {
        error = null;

        while (true)
        {
            try
            {
                if (!File.Exists(path)) return true;

                if (!SendToBin)
                {
                    // Asked for explicitly in Settings. No fallback and no
                    // second-guessing: the file is gone.
                    File.Delete(path);
                    return true;
                }

                // OnlyErrorDialogs, not AllDialogs: the caller has already
                // asked (or the user chose not to be asked), so a second
                // confirmation would be the app second-guessing its settings.
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                return true;
            }
            catch (IOException ex)
            {
                error = ex.Message;

                if (sleep == null || DateTime.UtcNow >= deadline) return false;
                sleep(PollMs);
            }
            catch (Exception recycleFailure)
            {
                // Not a lock — most likely a volume with no Recycle Bin, such
                // as a network share or removable media. The user still asked
                // for the file to go.
                try
                {
                    File.Delete(path);
                    return true;
                }
                catch (Exception ex)
                {
                    error = string.IsNullOrWhiteSpace(recycleFailure.Message)
                        ? ex.Message
                        : recycleFailure.Message;
                    return false;
                }
            }
        }
    }
}
