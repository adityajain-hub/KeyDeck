using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using KeyDeck.Platform.Linux.Native;

namespace KeyDeck.Platform.Linux.VirtualKeyboard;

/// <summary>
/// Creates and manages a virtual keyboard device via Linux /dev/uinput.
/// Synthesizes keystrokes, chords, and media events across Wayland, X11, and TTY.
/// </summary>
public sealed class UInputVirtualKeyboard : IDisposable
{
    private int _uinputFd = -1;
    private readonly object _writeLock = new();
    private bool _isDisposed;

    public bool IsInitialized => _uinputFd >= 0;

    public UInputVirtualKeyboard()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Don't initialize if running on non-Linux OS (e.g. while building or testing on Windows)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Trace.WriteLine("[UInputVirtualKeyboard] Skipping initialization on non-Linux platform.");
            return;
        }

        try
        {
            _uinputFd = NativeLibc.open("/dev/uinput", NativeLibc.O_WRONLY | NativeLibc.O_NONBLOCK | NativeLibc.O_CLOEXEC);
            if (_uinputFd < 0)
            {
                // Fallback to older /dev/input/uinput path
                _uinputFd = NativeLibc.open("/dev/input/uinput", NativeLibc.O_WRONLY | NativeLibc.O_NONBLOCK | NativeLibc.O_CLOEXEC);
            }

            if (_uinputFd < 0)
            {
                var err = Marshal.GetLastPInvokeError();
                Trace.WriteLine($"[UInputVirtualKeyboard] Failed to open /dev/uinput (errno={err}). Ensure user is in 'input' group.");
                return;
            }

            // Enable key events and sync
            NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_SET_EVBIT, (int)LinuxInputCodes.EV_KEY);
            NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_SET_EVBIT, (int)LinuxInputCodes.EV_SYN);
            NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_SET_EVBIT, (int)LinuxInputCodes.EV_REP);

            // Register standard keys (1 to 255)
            for (ushort k = 1; k < 255; k++)
            {
                NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_SET_KEYBIT, (int)k);
            }

            var setup = new uinput_setup
            {
                id = new input_id
                {
                    bustype = 0x03, // BUS_USB
                    vendor  = 0x1234,
                    product = 0x5678,
                    version = 1
                },
                name = "KeyDeck Virtual Macro Keyboard",
                ff_effects_max = 0
            };

            if (NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_DEV_SETUP, ref setup) < 0 ||
                NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_DEV_CREATE, 0) < 0)
            {
                var err = Marshal.GetLastPInvokeError();
                Trace.WriteLine($"[UInputVirtualKeyboard] Failed to register uinput device (errno={err}). Check udev permissions.");
                NativeLibc.close(_uinputFd);
                _uinputFd = -1;
                return;
            }

            Trace.WriteLine("[UInputVirtualKeyboard] Virtual keyboard successfully registered via /dev/uinput.");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UInputVirtualKeyboard] Error initializing uinput: {ex.Message}");
            if (_uinputFd >= 0)
            {
                NativeLibc.close(_uinputFd);
                _uinputFd = -1;
            }
        }
    }

    /// <summary>
    /// Writes a single input event followed by a SYN_REPORT.
    /// </summary>
    public unsafe void EmitKey(ushort code, int value)
    {
        if (_uinputFd < 0) return;

        lock (_writeLock)
        {
            var ev = new input_event
            {
                type  = LinuxInputCodes.EV_KEY,
                code  = code,
                value = value
            };

            NativeLibc.write(_uinputFd, &ev, (nuint)sizeof(input_event));

            var syn = new input_event
            {
                type  = LinuxInputCodes.EV_SYN,
                code  = LinuxInputCodes.SYN_REPORT,
                value = 0
            };

            NativeLibc.write(_uinputFd, &syn, (nuint)sizeof(input_event));
        }
    }

    /// <summary>
    /// Emits a full keystroke (press, sync, hold delay, release, sync).
    /// </summary>
    public async Task SendKeyStrokeAsync(ushort code, int holdDelayMs = 20)
    {
        EmitKey(code, LinuxInputCodes.KEY_PRESS);
        if (holdDelayMs > 0)
        {
            await Task.Delay(holdDelayMs).ConfigureAwait(false);
        }
        EmitKey(code, LinuxInputCodes.KEY_RELEASE);
    }

    /// <summary>
    /// Synthesizes a key chord (e.g. Ctrl + Shift + Esc).
    /// Presses all keys in order, holds for <paramref name="holdDelayMs"/>, and releases in reverse order.
    /// </summary>
    public async Task SendChordAsync(IReadOnlyList<ushort> keys, int holdDelayMs = 25)
    {
        if (keys == null || keys.Count == 0) return;

        // Press chord keys in forward order
        for (var i = 0; i < keys.Count; i++)
        {
            EmitKey(keys[i], LinuxInputCodes.KEY_PRESS);
        }

        await Task.Delay(holdDelayMs).ConfigureAwait(false);

        // Release chord keys in reverse order
        for (var i = keys.Count - 1; i >= 0; i--)
        {
            EmitKey(keys[i], LinuxInputCodes.KEY_RELEASE);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_uinputFd >= 0)
        {
            try
            {
                NativeLibc.ioctl(_uinputFd, LinuxInputCodes.UI_DEV_DESTROY, 0);
                NativeLibc.close(_uinputFd);
                _uinputFd = -1;
            }
            catch { /* non-critical during teardown */ }
        }
    }
}
