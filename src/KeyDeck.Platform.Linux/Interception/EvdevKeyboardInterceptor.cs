using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Platform.Linux.Native;
using KeyDeck.Platform.Linux.VirtualKeyboard;

namespace KeyDeck.Platform.Linux.Interception;

/// <summary>
/// Intercepts physical keyboard and numpad events at the Linux kernel evdev layer.
/// Operates universally across Wayland, X11, and TTY with zero latency.
/// Uses EVIOCGRAB when in Macro Mode to suppress native numpad events,
/// and re-emits non-numpad keystrokes through /dev/uinput.
/// </summary>
public sealed class EvdevKeyboardInterceptor : IKeyInterceptor
{
    private readonly AppSettings _settings;
    private readonly INumLockMonitor? _numLockMonitor;
    private readonly UInputVirtualKeyboard _virtualKeyboard;

    private readonly List<int> _deviceFds = new();
    private readonly Dictionary<int, string> _devicePaths = new();
    private readonly HashSet<int> _grabbedFds = new();
    private readonly object _deviceLock = new();

    private Thread? _workerThread;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;
    private bool _isMacroModeActive;

    public bool IsTemporaryBypassEnabled { get; set; }

    public Func<NumpadKeyId, bool>? AllowKeyRepeatPredicate { get; set; }

    public bool IsMacroModeActive => _isMacroModeActive;

    public event Action<NumpadKeyId>? NumpadKeyPressed;
    public event Action<NumpadKeyId>? NumpadKeyReleased;
    public event Action<bool>? MacroModeStateChanged;

    public EvdevKeyboardInterceptor(
        AppSettings settings,
        INumLockMonitor? numLockMonitor = null,
        UInputVirtualKeyboard? virtualKeyboard = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _numLockMonitor = numLockMonitor;
        _virtualKeyboard = virtualKeyboard ?? new UInputVirtualKeyboard();

        if (_numLockMonitor != null)
        {
            _numLockMonitor.NumLockChanged += OnNumLockStateChanged;
        }

        UpdateMacroState();
    }

    public void Start()
    {
        if (_workerThread != null) return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Trace.WriteLine("[EvdevKeyboardInterceptor] Skipping start on non-Linux platform.");
            return;
        }

        _cts = new CancellationTokenSource();
        _workerThread = new Thread(WorkerLoop)
        {
            Name = "KeyDeck.EvdevInterceptor",
            IsBackground = true
        };
        _workerThread.Start();
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _workerThread?.Join(500);
            _workerThread = null;
            _cts.Dispose();
            _cts = null;
        }

        ReleaseAllGrabs();
        CloseAllDevices();
    }

    public void NotifyMacroState()
    {
        UpdateMacroState();
    }

    private void OnNumLockStateChanged(bool isNumLockOn)
    {
        UpdateMacroState();
    }

    private void UpdateMacroState()
    {
        var isNumLockOn = _numLockMonitor?.IsNumLockOn ?? false;
        var newMacroActive = _settings.IsMacroEnabled &&
            (_settings.NumLockMode == NumLockMacroMode.OffMeansMacro ? !isNumLockOn : isNumLockOn);

        if (IsTemporaryBypassEnabled)
        {
            newMacroActive = false;
        }

        if (newMacroActive != _isMacroModeActive)
        {
            _isMacroModeActive = newMacroActive;
            Trace.WriteLine($"[EvdevKeyboardInterceptor] MacroMode transitioned to: {_isMacroModeActive}");

            if (_isMacroModeActive)
            {
                GrabAllDevices();
            }
            else
            {
                ReleaseAllGrabs();
            }

            MacroModeStateChanged?.Invoke(_isMacroModeActive);
        }
    }

    private static unsafe bool IsOwnVirtualDevice(int fd)
    {
        LinuxInputCodes.InputId id;
        return NativeLibc.ioctl(fd, LinuxInputCodes.EVIOCGID, &id) == 0 &&
               id.Vendor == 0x1234 && id.Product == 0x5678;
    }

    private static unsafe bool HasNumpadKeys(int fd)
    {
        const int len = 96; // KEY_MAX/8 + 1
        var bits = stackalloc byte[len];
        // EVIOCGBIT(EV_KEY, len) = _IOC(_IOC_READ, 'E', 0x20 + EV_KEY, len)
        ulong req = (2UL << 30) | ((ulong)len << 16) | ((ulong)'E' << 8) | (0x20 + LinuxInputCodes.EV_KEY);
        if (NativeLibc.ioctl(fd, req, bits) < 0) return false;
        bool Has(int code) => (bits[code / 8] & (1 << (code % 8))) != 0;
        return Has(LinuxInputCodes.KEY_KP0) || Has(LinuxInputCodes.KEY_KPENTER);
    }

    private void DiscoverKeyboardDevices()
    {
        lock (_deviceLock)
        {
            CloseAllDevices();

            try
            {
                if (!Directory.Exists("/dev/input")) return;
                var eventFiles = Directory.GetFiles("/dev/input", "event*");
                foreach (var file in eventFiles)
                {
                    var fd = NativeLibc.open(file, NativeLibc.O_RDONLY | NativeLibc.O_NONBLOCK | NativeLibc.O_CLOEXEC);
                    if (fd < 0) continue;

                    // Filter out KeyDeck's own uinput virtual device and non-numpad pointer devices
                    if (IsOwnVirtualDevice(fd) || !HasNumpadKeys(fd))
                    {
                        NativeLibc.close(fd);
                        continue;
                    }

                    _deviceFds.Add(fd);
                    _devicePaths[fd] = file;
                    Trace.WriteLine($"[EvdevKeyboardInterceptor] Opened keyboard device: {file} (fd={fd})");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EvdevKeyboardInterceptor] Device discovery failed: {ex.Message}");
            }
        }
    }

    private unsafe void WorkerLoop()
    {
        DiscoverKeyboardDevices();

        if (_isMacroModeActive)
        {
            GrabAllDevices();
        }

        var ct = _cts?.Token ?? CancellationToken.None;

        while (!ct.IsCancellationRequested && !_isDisposed)
        {
            int count;
            pollfd[] pollFds;

            lock (_deviceLock)
            {
                if (_deviceFds.Count == 0)
                {
                    Thread.Sleep(1000);
                    DiscoverKeyboardDevices();
                    continue;
                }

                count = _deviceFds.Count;
                pollFds = new pollfd[count];
                for (var i = 0; i < count; i++)
                {
                    pollFds[i] = new pollfd
                    {
                        fd = _deviceFds[i],
                        events = NativeLibc.POLLIN,
                        revents = 0
                    };
                }
            }

            int ret;
            fixed (pollfd* pFds = pollFds)
            {
                ret = NativeLibc.poll(pFds, (uint)count, 100);
            }

            if (ret <= 0) continue;

            var deadFds = new List<int>();

            for (var i = 0; i < count; i++)
            {
                var rev = pollFds[i].revents;
                if ((rev & (NativeLibc.POLLERR | NativeLibc.POLLHUP | 0x0020 /* POLLNVAL */)) != 0)
                {
                    deadFds.Add(pollFds[i].fd);
                    continue;
                }

                if ((rev & NativeLibc.POLLIN) == 0) continue;

                var fd = pollFds[i].fd;
                input_event ev;

                while (NativeLibc.read(fd, &ev, (nuint)sizeof(input_event)) == sizeof(input_event))
                {
                    ProcessEvent(ev);
                }
            }

            if (deadFds.Count > 0)
            {
                lock (_deviceLock)
                {
                    foreach (var fd in deadFds)
                    {
                        _grabbedFds.Remove(fd);
                        _deviceFds.Remove(fd);
                        _devicePaths.Remove(fd);
                        try { NativeLibc.close(fd); } catch { }
                        Trace.WriteLine($"[EvdevKeyboardInterceptor] Device unplugged/closed (fd={fd})");
                    }
                }
            }
        }
    }

    private void ProcessEvent(input_event ev)
    {
        if (ev.type != LinuxInputCodes.EV_KEY) return;

        var code = ev.code;
        var value = ev.value;

        // Check if this key is a numpad key
        if (LinuxInputCodes.TryMapToNumpadKey(code, out var numpadKeyId))
        {
            if (_isMacroModeActive && !IsTemporaryBypassEnabled)
            {
                // Key is intercepted!
                if (value == LinuxInputCodes.KEY_PRESS)
                {
                    NumpadKeyPressed?.Invoke(numpadKeyId);
                }
                else if (value == LinuxInputCodes.KEY_RELEASE)
                {
                    NumpadKeyReleased?.Invoke(numpadKeyId);
                }
                else if (value == LinuxInputCodes.KEY_REPEAT)
                {
                    // Handle repeat predicate
                    if (AllowKeyRepeatPredicate?.Invoke(numpadKeyId) == true)
                    {
                        NumpadKeyPressed?.Invoke(numpadKeyId);
                    }
                }
                return;
            }
        }

        // If this device was grabbed exclusively but a non-numpad key was pressed,
        // forward it to the virtual keyboard so typing is never blocked!
        if (_isMacroModeActive)
        {
            _virtualKeyboard.EmitKey(code, value);
        }
    }

    private void GrabAllDevices()
    {
        if (!_virtualKeyboard.IsInitialized)
        {
            Trace.WriteLine("[EvdevKeyboardInterceptor] uinput unavailable; refusing EVIOCGRAB to avoid blocking physical input.");
            return;
        }

        lock (_deviceLock)
        {
            foreach (var fd in _deviceFds)
            {
                if (!_grabbedFds.Contains(fd))
                {
                    var ret = NativeLibc.ioctl(fd, LinuxInputCodes.EVIOCGRAB, 1);
                    if (ret == 0)
                    {
                        _grabbedFds.Add(fd);
                    }
                    else
                    {
                        Trace.WriteLine($"[EvdevKeyboardInterceptor] EVIOCGRAB failed for fd={fd} (ret={ret})");
                    }
                }
            }
        }
    }

    private void ReleaseAllGrabs()
    {
        lock (_deviceLock)
        {
            foreach (var fd in _grabbedFds)
            {
                try
                {
                    NativeLibc.ioctl(fd, LinuxInputCodes.EVIOCGRAB, 0);
                }
                catch { /* best effort */ }
            }
            _grabbedFds.Clear();
        }
    }

    private void CloseAllDevices()
    {
        lock (_deviceLock)
        {
            ReleaseAllGrabs();
            foreach (var fd in _deviceFds)
            {
                try { NativeLibc.close(fd); } catch { }
            }
            _deviceFds.Clear();
            _devicePaths.Clear();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();
        _virtualKeyboard.Dispose();
    }
}
