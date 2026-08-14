using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Global hotkey hook for the "set bookmark timestamp" action. Supports a
/// single configurable binding (<see cref="HotkeyBinding"/>) that can be a
/// mouse button (MButton / XButton1 / XButton2) or a keyboard combo with
/// optional modifier keys (Ctrl / Shift / Alt / Win + Key).
/// </summary>
/// <remarks>
/// Uses low-level mouse + keyboard hooks (WH_MOUSE_LL / WH_KEYBOARD_LL).
/// The hook callbacks fire on a system thread, so we marshal everything
/// onto the UI dispatcher before raising <see cref="Triggered"/>.
/// </remarks>
public sealed class HotkeyService : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;

    // Mouse messages we care about
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    // X-button hi-word of wParam: 1 = XButton1, 2 = XButton2
    private const int XBUTTON1 = 0x0001;
    private const int XBUTTON2 = 0x0002;

    // Keyboard messages
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Virtual-key codes for modifier keys, used by GetAsyncKeyState.
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;     // ALT
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private LowLevelProc? _mouseProc;
    private LowLevelProc? _keyboardProc;
    private bool _disposed;

    // The hooks live on their own thread, NOT the UI thread. Windows delivers
    // a low-level hook callback to the thread that installed the hook, and it
    // blocks every mouse/key event system-wide until that thread pumps the
    // message and returns (up to LowLevelHooksTimeout, 300ms by default).
    // With the hooks on the UI thread, any UI-thread stall — a blocking
    // cross-process SendMessage to MPC-HC, a long layout pass — turned into
    // system-wide input lag. A dedicated thread that does nothing but pump
    // messages can't stall, so input stays smooth no matter what the UI does.
    private Thread? _hookThread;
    private uint _hookThreadId;

    /// <summary>Which hook class is currently installed, so a binding change to
    /// a different class can reinstall rather than silently keep the old one.</summary>
    private HotkeyBinding.HotkeyKind _installedKind = HotkeyBinding.HotkeyKind.None;
    private readonly ManualResetEventSlim _hooksInstalled = new(false);

    /// <summary>
    /// The UI dispatcher, captured at construction. Must be captured here
    /// rather than read inside the hook callback: the callback now runs on
    /// the hook thread, where <c>Dispatcher.CurrentDispatcher</c> would spin
    /// up a second dispatcher and marshal the event onto the wrong thread.
    /// </summary>
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    /// <summary>
    /// Single event that fires whenever the configured hotkey is pressed.
    /// Replaces the older <c>MiddleButtonPressed</c> + <c>KeyboardHotkeyPressed</c> pair.
    /// </summary>
    public event Action? Triggered;

    /// <summary>
    /// The current binding. Setting this updates which input triggers
    /// <see cref="Triggered"/>. Set to <see cref="HotkeyBinding.None"/>
    /// to disable the hotkey entirely (the hooks stay installed but no
    /// event will fire).
    /// </summary>
    public HotkeyBinding Binding { get; set; } = HotkeyBinding.DefaultMouse;

    public bool IsHookActive => _mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero;

    /// <summary>
    /// Longest time any single hook callback has taken, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Windows blocks every mouse and keyboard event system-wide until the
    /// hook callback returns (up to LowLevelHooksTimeout, 300ms by default).
    /// If this value stays near zero the hooks are exonerated as a cause of
    /// system-wide input lag; if it climbs, they are implicated.
    /// </remarks>
    public static long MaxCallbackMs;

    /// <summary>
    /// Worst time spent in <em>our own</em> callback logic, excluding the
    /// <c>CallNextHookEx</c> handoff.
    /// </summary>
    /// <remarks>
    /// Splitting the two answers who is responsible when the total reaches the
    /// 300ms LowLevelHooksTimeout:
    /// <list type="bullet">
    ///   <item><description>own high  → our managed callback stalled (GC
    ///   suspension, JIT, scheduling) and the fix is to leave the hook chain
    ///   entirely.</description></item>
    ///   <item><description>own ~0 but chain high → a low-level mouse hook
    ///   belonging to another program on this machine is slow, and removing
    ///   ours would not fix the lag.</description></item>
    /// </list>
    /// </remarks>
    public static long MaxOwnMs;

    /// <summary>Worst time spent inside <c>CallNextHookEx</c> — i.e. downstream hooks.</summary>
    public static long MaxChainMs;

    /// <summary>
    /// Set <c>MPCHC_EDITOR_NO_HOOKS=1</c> to start with the global input hooks
    /// disabled. This exists purely as an A/B test: run once with it set and
    /// once without, and compare how the mouse feels. Hooks are the only thing
    /// this app does that can degrade input outside its own window, so if the
    /// lag is identical with them off, the cause is elsewhere.
    /// </summary>
    /// <summary>
    /// Raised on a bare <c>X</c> keypress, to bring the full window back from
    /// the minimal overlay.
    /// </summary>
    /// <remarks>
    /// No check for whether another application also handles that key, and the
    /// keypress is not swallowed — it carries on to whatever else wants it.
    /// It only listens while <see cref="RestoreArmed"/> is set, because a
    /// single unmodified letter firing at all times would mean typing an "x"
    /// anywhere on the machine yanked the window back.
    /// </remarks>
    public event Action? RestoreRequested;

    /// <summary>
    /// Whether the <c>X</c> restore key is live. Set for exactly as long as the
    /// overlay is on screen — which is not the same as for as long as the
    /// overlay exists, since a pinned one hides itself while another
    /// application is in front. The caller keeps the two in step; see
    /// <c>MainViewModel.SetOverlayShown</c>.
    /// </summary>
    public bool RestoreArmed { get; set; }

    public static bool HooksDisabledByEnvironment =>
        Environment.GetEnvironmentVariable("MPCHC_EDITOR_NO_HOOKS") == "1";

    /// <summary>
    /// Installs the hooks on a dedicated background thread that runs nothing
    /// but a message pump, so hook callbacks are always serviced immediately
    /// regardless of what the UI thread is doing.
    /// </summary>
    public void Start()
    {
        if (HooksDisabledByEnvironment) return;

        // If a thread is already running but for the wrong hook class (the user
        // switched between a mouse and a keyboard binding), tear it down so the
        // correct one gets installed.
        if (_hookThread != null)
        {
            if (_installedKind == Binding.Kind) return;
            Stop();
        }

        _hooksInstalled.Reset();

        _hookThread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "HotkeyHookPump",
            // Above normal so the pump is scheduled promptly even when ffmpeg
            // is saturating the CPU — a late callback is system-wide input lag.
            Priority = ThreadPriority.AboveNormal
        };
        _hookThread.Start();

        // Wait briefly for installation so IsHookActive is meaningful to the
        // caller (it drives the "Hotkey: …" status text).
        _hooksInstalled.Wait(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Body of the hook thread: install both hooks, then pump messages until
    /// <see cref="Stop"/> posts WM_QUIT. The pump is required — low-level
    /// hook callbacks are delivered as messages to this thread.
    /// </summary>
    private void HookThreadProc()
    {
        _hookThreadId = GetCurrentThreadId();

        // Install ONLY the hook the current binding needs. A low-level hook
        // interposes on the OS input path — every event of that class in the
        // whole system waits on our callback returning — so installing one we
        // will never act on is pure system-wide cost. Previously both went in
        // regardless, meaning a mouse binding still routed every keystroke on
        // the machine through a managed callback that could only ignore it.
        var kind = Binding.Kind;

        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule!)
        {
            var hMod = GetModuleHandle(curModule.ModuleName);

            if (kind == HotkeyBinding.HotkeyKind.Mouse)
            {
                _mouseProc ??= MouseHookCallback;
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
            }

            // The keyboard hook goes in regardless of the binding kind: it also
            // carries the X restore key, which brings the window back from the
            // minimal overlay and has to work even when the timestamp hotkey is
            // a mouse button. The callback returns immediately for anything else.
            _keyboardProc ??= KeyboardHookCallback;
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        }

        _installedKind = kind;

        _hooksInstalled.Set();

        // GetMessage blocks until a message arrives and returns 0 on WM_QUIT.
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Unhook on the same thread that installed the hooks.
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    public void Stop()
    {
        var thread = _hookThread;
        if (thread == null) return;

        _hookThread = null;

        if (_hookThreadId != 0)
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);

        // Don't block shutdown indefinitely if the pump is wedged; the thread
        // is IsBackground so the process can exit regardless.
        thread.Join(TimeSpan.FromSeconds(2));
        _hookThreadId = 0;
        _installedKind = HotkeyBinding.HotkeyKind.None;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var startTicks = Stopwatch.GetTimestamp();
        try
        {
            return MouseHookCallbackCore(nCode, wParam, lParam, startTicks);
        }
        finally
        {
            RecordCallbackDuration(startTicks);
        }
    }

    /// <summary>
    /// Tracks the worst-case callback cost. Every mouse and key event in the
    /// OS waits on this returning, so it must stay in the microseconds.
    /// </summary>
    private static void RecordCallbackDuration(long startTicks)
        => RecordMax(ref MaxCallbackMs, startTicks);

    private static void RecordMax(ref long target, long startTicks)
    {
        var ms = (Stopwatch.GetTimestamp() - startTicks) * 1000 / Stopwatch.Frequency;
        if (ms > Interlocked.Read(ref target))
            Interlocked.Exchange(ref target, ms);
    }

    /// <summary>
    /// Hands off to the next hook in the chain, timing it separately. This is
    /// the only call in the callback that can block on code we do not own.
    /// </summary>
    private static IntPtr CallNextTimed(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam)
    {
        var start = Stopwatch.GetTimestamp();
        try { return CallNextHookEx(hook, nCode, wParam, lParam); }
        finally { RecordMax(ref MaxChainMs, start); }
    }

    private IntPtr MouseHookCallbackCore(int nCode, IntPtr wParam, IntPtr lParam, long startTicks)
    {
        if (nCode >= 0 && Binding.Kind == HotkeyBinding.HotkeyKind.Mouse)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_MBUTTONDOWN && Binding.Mouse == HotkeyBinding.MouseButtonKind.MButton)
            {
                FireTriggered();
            }
            else if (msg == WM_XBUTTONDOWN)
            {
                // For X-button messages, lParam's hi-word holds the
                // X-button number (1 or 2). The mouse hook struct is:
                //   POINT pt  (low word / high word of lParam's low int)
                //   int mouseData  (contains the X-button number in hi-word)
                int mouseData = unchecked((int)((long)lParam >> 32));
                int xButton = (mouseData >> 16) & 0xFFFF;
                if (xButton == XBUTTON1 && Binding.Mouse == HotkeyBinding.MouseButtonKind.XButton1)
                    FireTriggered();
                else if (xButton == XBUTTON2 && Binding.Mouse == HotkeyBinding.MouseButtonKind.XButton2)
                    FireTriggered();
            }
        }

        // Our own work ends here; anything past this point belongs to the next
        // hook in the chain, which may be owned by another program entirely.
        RecordMax(ref MaxOwnMs, startTicks);
        return CallNextTimed(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var startTicks = Stopwatch.GetTimestamp();
        try
        {
            return KeyboardHookCallbackCore(nCode, wParam, lParam);
        }
        finally
        {
            RecordCallbackDuration(startTicks);
        }
    }

    private IntPtr KeyboardHookCallbackCore(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            // X restores the full window from the minimal overlay — only while
            // that overlay is up. See RestoreRequested.
            if (RestoreArmed)
            {
                var restoreVk = Marshal.ReadInt32(lParam);
                if (KeyInterop.KeyFromVirtualKey(restoreVk) == Key.X)
                    RaiseRestoreRequested();
            }
        }

        if (nCode >= 0 && Binding.Kind == HotkeyBinding.HotkeyKind.Keyboard &&
            (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);
            var key = KeyInterop.KeyFromVirtualKey(vk);

            // Modifier keys alone shouldn't trigger — wait for an actual
            // key. (Pressing just "Ctrl" by itself shouldn't fire.)
            if (IsModifierKey(key)) return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

            if (key == Binding.Key)
            {
                ModifierKeys pressed = ReadCurrentModifiers();
                if (pressed == Binding.Modifiers)
                    FireTriggered();
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Reads the current state of the modifier keys via GetAsyncKeyState.
    /// This is more reliable than WPF's Keyboard.Modifiers from a
    /// low-level hook callback (which may run on a non-UI thread).
    /// </summary>
    private static ModifierKeys ReadCurrentModifiers()
    {
        ModifierKeys mods = ModifierKeys.None;
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods |= ModifierKeys.Control;
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) mods |= ModifierKeys.Shift;
        if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0) mods |= ModifierKeys.Alt;
        if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
            (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0) mods |= ModifierKeys.Windows;
        return mods;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;

    /// <summary>
    /// Marshal the trigger event onto the UI thread. Uses the dispatcher
    /// captured at construction — the callback runs on the hook thread, so
    /// <c>Dispatcher.CurrentDispatcher</c> would be the wrong one. Posting
    /// (rather than invoking) keeps the hook callback returning immediately,
    /// which is what prevents system-wide input lag.
    /// </summary>
    private void FireTriggered()
        => _uiDispatcher.BeginInvoke(() => Triggered?.Invoke());

    /// <summary>
    /// Marshals <see cref="RestoreRequested"/> onto the UI thread, same as
    /// <see cref="FireTriggered"/>. Debounced against key auto-repeat, which
    /// would otherwise fire this dozens of times per second while the key is
    /// held down.
    /// </summary>
    private DateTime _lastRestoreUtc = DateTime.MinValue;

    private void RaiseRestoreRequested()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRestoreUtc).TotalMilliseconds < 400) return;
        _lastRestoreUtc = now;

        _uiDispatcher.BeginInvoke(() => RestoreRequested?.Invoke());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _hooksInstalled.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Win32

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    #endregion
}
