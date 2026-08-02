using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Diagnostic: detects UI-thread stalls and attributes them.
/// </summary>
/// <remarks>
/// <para>
/// The UI thread updates a heartbeat timestamp on a fast dispatcher timer. A
/// dedicated <em>background</em> watchdog thread watches that timestamp, so a
/// stall is noticed while it is still happening rather than after the fact —
/// a dispatcher timer cannot report its own starvation.
/// </para>
/// <para>
/// Attribution comes from three sources, so no call site needs to be wrapped
/// by hand:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="DispatcherHooks"/> names the dispatcher
///   operation in flight (resolved to Type.Method).</description></item>
///   <item><description>GC pause time consumed across the stall, which
///   separates a blocking collection from code that is genuinely
///   blocked.</description></item>
///   <item><description>Whether the app had focus, to correlate with the
///   refocus case.</description></item>
/// </list>
/// <para>
/// Log lives next to the exe as <c>stalls.log</c>; delete it to reset. All
/// writing happens on a background thread — the previous version wrote the
/// log from the UI thread, which added to the very problem it measured.
/// </para>
/// </remarks>
public sealed class StallMonitor : IDisposable
{
    /// <summary>How often the UI thread refreshes its heartbeat.</summary>
    private const int HeartbeatMs = 50;

    /// <summary>Heartbeat gap beyond which the UI thread counts as stalled.</summary>
    private const int StallThresholdMs = 150;

    private readonly DispatcherTimer _heartbeat;
    private readonly Thread _watchdog;
    private readonly Thread _writer;
    private readonly BlockingCollection<string> _pending = new(new ConcurrentQueue<string>());
    private readonly string _logPath;
    private readonly CancellationTokenSource _cts = new();

    private long _lastBeatMs = Environment.TickCount64;
    private string _currentOperation = "(idle)";
    private string _manualMark = "";
    private bool _disposed;

    // A stall reported as op=(idle) means the UI thread was NOT inside a
    // dispatcher work item — it was inside raw Win32 message handling, which
    // DispatcherHooks cannot see. Tracking the last message pumped, plus how
    // many were pumped during the stall, separates the two possible shapes:
    //   messages≈0  -> blocked inside a single handler (a synchronous paint,
    //                  a blocking SendMessage from a WndProc, a modal loop)
    //   messages≫0  -> a flood (mouse moves, timer storm) starving the thread
    private volatile int _lastMessage;
    private long _messageCount;
    private volatile int _renderCount;

    // Ring buffer of recent messages. msgAtStart alone can be misleading — the
    // watchdog samples it up to a heartbeat late — so keep a short history to
    // show what led into the stall.
    private readonly int[] _recentMessages = new int[12];
    private int _recentIndex;

    /// <summary>
    /// True while Windows is running one of its own nested modal loops
    /// (caption drag / resize / menu tracking). During those, DefWindowProc
    /// pumps messages privately and WPF's dispatcher queue is not serviced, so
    /// the heartbeat legitimately stops. Stalls flagged this way are Windows
    /// behaving normally, not the app blocking, and must not be confused with
    /// each other.
    /// </summary>
    private volatile bool _inModalLoop;

    // DispatcherOperation does not expose the delegate it will run, but it
    // holds one privately. Reading it turns "(idle)" into an actual method
    // name. Reflection on a private field is acceptable for a diagnostic and
    // degrades to "unknown" if the field is ever renamed.
    private static readonly FieldInfo? OperationMethodField =
        typeof(DispatcherOperation).GetField("_method",
            BindingFlags.NonPublic | BindingFlags.Instance);

    public StallMonitor()
    {
        _logPath = Path.Combine(MpcHcVideoEditor.Helpers.PortablePaths.AppFolder, "stalls.log");

        _writer = new Thread(WriterProc)
        {
            IsBackground = true,
            Name = "StallLogWriter"
        };
        _writer.Start();

        Write($"--- session start {DateTime.Now:yyyy-MM-dd HH:mm:ss} " +
              $"(heartbeat {HeartbeatMs}ms, threshold {StallThresholdMs}ms) ---");

        // Rendering tier matters: in software mode (tier 0) WPF rasterises on
        // the UI thread, so a full repaint — such as the one that follows
        // window activation — becomes UI-thread CPU time and shows up as an
        // (idle) stall. 0x20000 = full hardware, 0x10000 = partial, 0 = software.
        Write($"renderTier={RenderCapability.Tier >> 16}  " +
              $"processRenderMode={RenderOptions.ProcessRenderMode}  " +
              $"cores={Environment.ProcessorCount}");

        var dispatcher = Dispatcher.CurrentDispatcher;

        // Name whatever the dispatcher is running, so an otherwise anonymous
        // stall gets a method attached to it.
        try
        {
            dispatcher.Hooks.OperationStarted += (_, e) => _currentOperation = Describe(e.Operation);
            dispatcher.Hooks.OperationCompleted += (_, _) => _currentOperation = "(idle)";
            dispatcher.Hooks.OperationAborted += (_, _) => _currentOperation = "(idle)";
        }
        catch
        {
            // Hooks are best-effort; the watchdog still reports durations.
        }

        // Every Win32 message the UI thread pumps passes through here. This is
        // the only vantage point that can attribute an (idle) stall.
        ComponentDispatcher.ThreadFilterMessage += (ref MSG msg, ref bool handled) =>
        {
            _lastMessage = msg.message;
            Interlocked.Increment(ref _messageCount);

            var slot = _recentIndex++ % _recentMessages.Length;
            _recentMessages[slot] = msg.message;

            switch (msg.message)
            {
                case 0x0231: // WM_ENTERSIZEMOVE
                case 0x0211: // WM_ENTERMENULOOP
                case 0x00A1: // WM_NCLBUTTONDOWN — DefWindowProc may enter SC_MOVE
                    _inModalLoop = true;
                    break;
                case 0x0232: // WM_EXITSIZEMOVE
                case 0x0212: // WM_EXITMENULOOP
                case 0x00A2: // WM_NCLBUTTONUP
                    _inModalLoop = false;
                    break;
            }
        };

        // Counts composition passes so a stall can be checked against whether
        // rendering was progressing at all while it happened.
        CompositionTarget.Rendering += (_, _) => _renderCount++;

        _heartbeat = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(HeartbeatMs)
        };
        _heartbeat.Tick += (_, _) => _lastBeatMs = Environment.TickCount64;
        _heartbeat.Start();

        _watchdog = new Thread(WatchdogProc)
        {
            IsBackground = true,
            Name = "StallWatchdog",
            Priority = ThreadPriority.AboveNormal
        };
        _watchdog.Start();
    }

    private static string Describe(DispatcherOperation op)
    {
        try
        {
            if (OperationMethodField?.GetValue(op) is Delegate d && d.Method is { } m)
            {
                // A lambda's declaring type is the compiler-generated closure
                // class ("<>c"), which on its own says nothing. Walk out to the
                // real enclosing type so the log names something searchable.
                var type = m.DeclaringType;
                while (type != null && type.Name.StartsWith('<') && type.DeclaringType != null)
                    type = type.DeclaringType;

                return $"{type?.Name}.{m.Name}";
            }
        }
        catch
        {
            // fall through
        }
        return "unknown";
    }

    /// <summary>Oldest-to-newest dump of the recent-message ring buffer.</summary>
    private string RecentMessages()
    {
        var start = _recentIndex;
        var names = new List<string>(_recentMessages.Length);
        for (int i = 0; i < _recentMessages.Length; i++)
        {
            var value = _recentMessages[(start + i) % _recentMessages.Length];
            if (value != 0) names.Add(MessageName(value));
        }
        return string.Join(" ", names);
    }

    /// <summary>Names the common Win32 messages so the log is readable.</summary>
    private static string MessageName(int msg) => msg switch
    {
        0x0006 => "WM_ACTIVATE",
        0x0007 => "WM_SETFOCUS",
        0x0008 => "WM_KILLFOCUS",
        0x000F => "WM_PAINT",
        0x0014 => "WM_ERASEBKGND",
        0x0018 => "WM_SHOWWINDOW",
        0x0046 => "WM_WINDOWPOSCHANGING",
        0x0047 => "WM_WINDOWPOSCHANGED",
        0x0084 => "WM_NCHITTEST",
        0x0086 => "WM_NCACTIVATE",
        0x0113 => "WM_TIMER",
        0x0200 => "WM_MOUSEMOVE",
        0x0201 => "WM_LBUTTONDOWN",
        0x0202 => "WM_LBUTTONUP",
        0x020A => "WM_MOUSEWHEEL",
        0x0281 => "WM_IME_SETCONTEXT",
        0x02E0 => "WM_DPICHANGED",
        0x0318 => "WM_PRINTCLIENT",
        0x00A0 => "WM_NCMOUSEMOVE",
        0x00A1 => "WM_NCLBUTTONDOWN",
        0x00A2 => "WM_NCLBUTTONUP",
        0x0021 => "WM_MOUSEACTIVATE",
        0x001C => "WM_ACTIVATEAPP",
        0x0231 => "WM_ENTERSIZEMOVE",
        0x0232 => "WM_EXITSIZEMOVE",
        0x0211 => "WM_ENTERMENULOOP",
        0x0212 => "WM_EXITMENULOOP",
        0x0112 => "WM_SYSCOMMAND",
        0x0215 => "WM_CAPTURECHANGED",
        _ => $"0x{msg:X4}"
    };

    /// <summary>
    /// Watches the heartbeat from outside the UI thread. Reports each stall
    /// once, when it ends, with the work in flight and the GC time consumed
    /// while it lasted.
    /// </summary>
    private void WatchdogProc()
    {
        var token = _cts.Token;
        bool inStall = false;
        long stallStartMs = 0;
        TimeSpan gcPauseAtStallStart = TimeSpan.Zero;
        int gen2AtStallStart = 0;
        string operationAtStallStart = "";
        bool focusedAtStallStart = false;
        int messageAtStallStart = 0;
        long messageCountAtStallStart = 0;
        int renderCountAtStallStart = 0;
        bool modalAtStallStart = false;

        while (!token.IsCancellationRequested)
        {
            var gap = Environment.TickCount64 - _lastBeatMs;

            if (!inStall && gap >= StallThresholdMs)
            {
                inStall = true;
                stallStartMs = _lastBeatMs;
                gcPauseAtStallStart = GC.GetTotalPauseDuration();
                gen2AtStallStart = GC.CollectionCount(2);
                operationAtStallStart = _currentOperation;
                focusedAtStallStart = _lastKnownFocus;
                messageAtStallStart = _lastMessage;
                messageCountAtStallStart = Interlocked.Read(ref _messageCount);
                renderCountAtStallStart = _renderCount;
                modalAtStallStart = _inModalLoop;
            }
            else if (inStall && gap < StallThresholdMs)
            {
                inStall = false;
                var duration = _lastBeatMs - stallStartMs;
                var gcPause = (GC.GetTotalPauseDuration() - gcPauseAtStallStart).TotalMilliseconds;
                var gen2 = GC.CollectionCount(2) - gen2AtStallStart;

                // Prefer the operation seen when the stall began; if that was
                // idle, fall back to whatever ran by the time it ended.
                var op = operationAtStallStart != "(idle)" ? operationAtStallStart : _currentOperation;
                var mark = string.IsNullOrEmpty(_manualMark) ? "" : $"  mark={_manualMark}";
                var messages = Interlocked.Read(ref _messageCount) - messageCountAtStallStart;
                var renders = _renderCount - renderCountAtStallStart;

                // A stall inside a Windows modal loop is expected, not a defect.
                // Label it so the log distinguishes "the app blocked" from
                // "the user was holding the title bar".
                var kind = modalAtStallStart ? "MODAL-LOOP (expected)" : "APP-BLOCKED";

                Write($"UI stall {duration}ms  [{kind}]  op={op}{mark}  " +
                      $"gcPause={gcPause:0}ms gen2={gen2}  " +
                      $"focused={focusedAtStallStart}  " +
                      $"msgAtStart={MessageName(messageAtStallStart)} " +
                      $"msgsDuring={messages} rendersDuring={renders}  " +
                      $"recent=[{RecentMessages()}]  " +
                      $"hookMaxMs={HotkeyService.MaxCallbackMs} " +
                      $"(own={HotkeyService.MaxOwnMs} chain={HotkeyService.MaxChainMs})");
            }

            Thread.Sleep(HeartbeatMs / 2);
        }
    }

    private volatile bool _lastKnownFocus;

    /// <summary>
    /// Records window focus so stalls can be correlated with the
    /// unfocus/refocus cycle. Call from Activated/Deactivated.
    /// </summary>
    /// <summary>Writes an arbitrary line into the log, for run context.</summary>
    public void Note(string message) => Write(message);

    public void NoteFocus(bool focused)
    {
        _lastKnownFocus = focused;
        Write($"focus={(focused ? "gained" : "lost")}");
    }

    /// <summary>
    /// Optional extra context for the next reported stall. Call with null when
    /// the work finishes.
    /// </summary>
    public void Mark(string? operation)
        => _manualMark = operation ?? "";

    /// <summary>
    /// Times a synchronous operation and logs it if it exceeds
    /// <paramref name="warnAboveMs"/>. Returns elapsed milliseconds.
    /// </summary>
    public long Time(string operation, Action work, long warnAboveMs = 50)
    {
        Mark(operation);
        var sw = Stopwatch.StartNew();
        try
        {
            work();
        }
        finally
        {
            sw.Stop();
            Mark(null);
            if (sw.ElapsedMilliseconds >= warnAboveMs)
                Write($"slow op {sw.ElapsedMilliseconds}ms  {operation}");
        }
        return sw.ElapsedMilliseconds;
    }

    private void Write(string line)
    {
        if (_disposed) return;
        try { _pending.Add($"{DateTime.Now:HH:mm:ss.fff}  {line}"); }
        catch { /* diagnostics must never break the app */ }
    }

    private void WriterProc()
    {
        try
        {
            foreach (var line in _pending.GetConsumingEnumerable())
            {
                try { File.AppendAllText(_logPath, line + Environment.NewLine); }
                catch { /* ignore log write failures */ }
            }
        }
        catch
        {
            // collection completed
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _heartbeat.Stop();
        _cts.Cancel();
        _pending.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }
}
