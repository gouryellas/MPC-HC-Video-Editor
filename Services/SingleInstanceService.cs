using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Enforces "only one copy of this install running at once", and hands a
/// blocked launch off to the copy that is already running.
/// </summary>
/// <remarks>
/// Scoped to the install folder (<see cref="PortablePaths.AppFolder"/>), not
/// to the application in general. BUILD.md's whole design is that a copy of
/// the folder is a fully independent, portable install with its own
/// settings.json — so two such copies running from two different folders are
/// not "the same instance" and must not block each other. Only a second
/// launch of the exact same folder counts.
///
/// A named <see cref="Mutex"/> decides who is first: cheap, and if the owning
/// process dies without releasing it, Windows releases it automatically, so a
/// crash can never leave a later launch permanently locked out. A named
/// <see cref="EventWaitHandle"/> carries the "someone else just tried to
/// start, come to the front" signal from a blocked launch to the running one.
///
/// This only ever inspects the setting once, at construction — deciding
/// which of several already-running copies should react to it being flipped
/// later has no good answer, so flipping it only changes what a later,
/// separate launch does.
/// </remarks>
public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _wakeEvent;

    /// <summary>
    /// Captured at construction, on the UI thread that constructs this
    /// service. The wake listener runs on its own thread, where
    /// <c>Dispatcher.CurrentDispatcher</c> would be the wrong one.
    /// </summary>
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    private bool _disposed;

    /// <summary>
    /// True if this process won the race to be the one running copy. False
    /// means another copy already holds it — the caller should hand off via
    /// <see cref="SignalRunningInstance"/> and exit without ever creating a
    /// window.
    /// </summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// Raised on the UI thread when a later, blocked launch asks this
    /// instance to come to the front. Never raised when
    /// <see cref="IsFirstInstance"/> is false.
    /// </summary>
    public event Action? WakeRequested;

    public SingleInstanceService()
    {
        var key = ComputeInstanceKey();

        // "Local\" rather than the default session namespace or "Global\":
        // explicit about being scoped to this login session, and it sidesteps
        // the extra privilege "Global\" can need under a locked-down Terminal
        // Services / Citrix policy. A single-user desktop editor has no
        // business reaching across sessions anyway.
        _mutex = new Mutex(initiallyOwned: true, name: $@"Local\MpcHcVideoEditor.Lock.{key}",
            createdNew: out var createdNew);
        IsFirstInstance = createdNew;

        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
            $@"Local\MpcHcVideoEditor.Wake.{key}");

        if (IsFirstInstance)
        {
            var thread = new Thread(ListenForWake)
            {
                IsBackground = true,
                Name = "SingleInstanceWakeListener"
            };
            thread.Start();
        }
    }

    /// <summary>
    /// Tells whichever copy is holding the lock that a second launch just
    /// happened. Only meaningful when <see cref="IsFirstInstance"/> is false;
    /// harmless otherwise, since nothing is listening on its own event.
    /// </summary>
    public void SignalRunningInstance()
    {
        try { _wakeEvent.Set(); }
        catch { /* the running copy just won't be raised to the front — not worth losing the launch decision over */ }
    }

    /// <summary>
    /// Runs on a dedicated thread for the process's whole lifetime. Wakes
    /// once a second to re-check <see cref="_disposed"/> rather than blocking
    /// on the handle forever, so shutdown does not also have to signal the
    /// event just to unstick this thread.
    /// </summary>
    private void ListenForWake()
    {
        while (!_disposed)
        {
            bool signalled;
            try { signalled = _wakeEvent.WaitOne(1000); }
            catch { return; }

            if (signalled && !_disposed)
                _uiDispatcher.BeginInvoke(() => WakeRequested?.Invoke());
        }
    }

    /// <summary>
    /// A short, filesystem-free, kernel-object-safe name derived from the
    /// install folder. Case-insensitive and trimmed, so the same folder
    /// reached via a trailing slash or different casing still hashes the same.
    /// </summary>
    private static string ComputeInstanceKey()
    {
        var folder = PortablePaths.AppFolder.TrimEnd('\\', '/').ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(folder));
        return Convert.ToHexString(hash, 0, 16);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { if (IsFirstInstance) _mutex.ReleaseMutex(); }
        catch { /* best-effort — Windows releases an abandoned mutex on process exit regardless */ }

        _mutex.Dispose();

        // _wakeEvent is deliberately not disposed here: the listener thread
        // may still be inside WaitOne on it, and disposing a handle out from
        // under a thread waiting on it is not safe. It is a background
        // thread holding a single OS handle, so letting process exit reclaim
        // both is simpler than coordinating a race-free handoff for a
        // shutdown path that runs exactly once.
    }
}
