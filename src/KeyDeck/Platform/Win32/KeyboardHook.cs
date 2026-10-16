using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure.Config;

namespace KeyDeck.Platform.Win32;

/// <summary>
/// Low-level Windows keyboard hook (WH_KEYBOARD_LL) specialized for hardware numpad interception.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly HashSet<NumpadKeyId> _swallowedKeys = new();
    private bool _isDisposed;

    /// <summary>
    /// Evaluates whether a given key should currently be intercepted and swallowed.
    /// </summary>
    public Func<NumpadKeyId, bool>? ShouldInterceptFilter { get; set; }

    /// <summary>
    /// Fired when an intercepted numpad key is pressed down.
    /// </summary>
    public event Action<NumpadKeyId>? KeyPressed;

    /// <summary>
    /// Fired when an intercepted numpad key is released.
    /// </summary>
    public event Action<NumpadKeyId>? KeyReleased;

    /// <summary>
    /// Fired when the physical Num Lock key is toggled.
    /// </summary>
    public event Action? NumLockToggled;

    public KeyboardHook()
    {
        // Pin delegate to instance field to prevent Garbage Collector from collecting unmanaged pointer
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            Trace.WriteLine($"Failed to install keyboard hook. Win32 error: {err}");
        }
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _swallowedKeys.Clear();
        }
    }

    private unsafe IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            // Zero-allocation: read struct directly from the unmanaged lParam pointer.
            // Marshal.PtrToStructure<T> copies to a new managed heap object on every keystroke;
            // this avoids that allocation entirely (valid because Win32 guarantees lParam
            // points to a valid KBDLLHOOKSTRUCT for the duration of the callback).
            ref readonly var hookStruct = ref Unsafe.AsRef<NativeMethods.KBDLLHOOKSTRUCT>((void*)lParam);

            // Rule A: Never intercept synthetic keys injected by SendInput or software.
            // LLKHF_INJECTED (0x10)        — any injected event.
            // LLKHF_LOWER_IL_INJECTED (0x02) — injected from a lower-integrity-level process
            //   (a known bypass: a low-IL process injects keys pretending to be physical).
            if ((hookStruct.flags & (NativeMethods.LLKHF_INJECTED | NativeMethods.LLKHF_LOWER_IL_INJECTED)) != 0)
            {
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Rule D: Physical Num Lock key (0x90). Always pass through so Windows updates hardware state
            if (hookStruct.vkCode == NativeMethods.VK_NUMLOCK)
            {
                if (wParam == (IntPtr)NativeMethods.WM_KEYUP || wParam == (IntPtr)NativeMethods.WM_SYSKEYUP)
                {
                    // Delay to allow Windows to update GetKeyState toggle state.
                    Task.Delay(AppConstants.NumLockSettleDelayMs).ContinueWith(
                        _ => NumLockToggled?.Invoke(),
                        TaskScheduler.Default);
                }
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Map physical hardware scan code to NumpadKeyId
            if (TryResolveNumpadKey(hookStruct.scanCode, hookStruct.flags, out var keyId))
            {
                var isDown = wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN;
                var isUp = wParam == (IntPtr)NativeMethods.WM_KEYUP || wParam == (IntPtr)NativeMethods.WM_SYSKEYUP;

                if (isDown)
                {
                    var shouldIntercept = ShouldInterceptFilter?.Invoke(keyId) ?? false;
                    if (shouldIntercept)
                    {
                        lock (_swallowedKeys)
                        {
                            _swallowedKeys.Add(keyId);
                        }

                        // Fire key event asynchronously so hook callback returns < 1ms
                        var pressedEvent = KeyPressed;
                        if (pressedEvent != null)
                        {
                            ThreadPool.QueueUserWorkItem(_ => pressedEvent.Invoke(keyId));
                        }

                        return (IntPtr)1; // Swallow key
                    }
                }
                else if (isUp)
                {
                    bool wasSwallowed;
                    lock (_swallowedKeys)
                    {
                        wasSwallowed = _swallowedKeys.Remove(keyId);
                    }

                    if (wasSwallowed)
                    {
                        var releasedEvent = KeyReleased;
                        if (releasedEvent != null)
                        {
                            ThreadPool.QueueUserWorkItem(_ => releasedEvent.Invoke(keyId));
                        }

                        return (IntPtr)1; // Swallow matching key-up
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool TryResolveNumpadKey(uint scanCode, uint flags, out NumpadKeyId keyId)
    {
        var isExtended = (flags & NativeMethods.LLKHF_EXTENDED) != 0;

        if (!isExtended)
        {
            // Rule B: Non-Extended Numpad scan codes
            switch (scanCode)
            {
                case 0x52: keyId = NumpadKeyId.Numpad0; return true;
                case 0x4F: keyId = NumpadKeyId.Numpad1; return true;
                case 0x50: keyId = NumpadKeyId.Numpad2; return true;
                case 0x51: keyId = NumpadKeyId.Numpad3; return true;
                case 0x4B: keyId = NumpadKeyId.Numpad4; return true;
                case 0x4C: keyId = NumpadKeyId.Numpad5; return true;
                case 0x4D: keyId = NumpadKeyId.Numpad6; return true;
                case 0x47: keyId = NumpadKeyId.Numpad7; return true;
                case 0x48: keyId = NumpadKeyId.Numpad8; return true;
                case 0x49: keyId = NumpadKeyId.Numpad9; return true;
                case 0x53: keyId = NumpadKeyId.NumpadDecimal; return true;
                case 0x4E: keyId = NumpadKeyId.NumpadAdd; return true;
                case 0x4A: keyId = NumpadKeyId.NumpadSubtract; return true;
                case 0x37: keyId = NumpadKeyId.NumpadMultiply; return true;
            }
        }
        else
        {
            // Rule C: Extended Numpad scan codes
            switch (scanCode)
            {
                case 0x35: keyId = NumpadKeyId.NumpadDivide; return true;
                case 0x1C: keyId = NumpadKeyId.NumpadEnter; return true;
            }
        }

        keyId = default;
        return false;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Uninstall();
            _isDisposed = true;
            GC.KeepAlive(_proc);
        }
    }
}
