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

    private void DiscoverKeyboardDevices()
    {
        CloseAllDevices();

        try
        {
            var eventFiles = Directory.GetFiles("/dev/input", "event*");
            foreach (var file in eventFiles)
            {
                var fd = NativeLibc.open(file, NativeLibc.O_RDONLY | NativeLibc.O_NONBLOCK | NativeLibc.O_CLOEXEC);
                if (fd >= 0)
                {
                    _deviceFds.Add(fd);
                    _devicePaths[fd] = file;
                    Trace.WriteLine($"[EvdevKeyboardInterceptor] Opened input device: {file} (fd={fd})");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[EvdevKeyboardInterceptor] Device discovery failed: {ex.Message}");
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
            if (_deviceFds.Count == 0)
            {
                Thread.Sleep(1000);
                DiscoverKeyboardDevices();
                continue;
            }

            var count = _deviceFds.Count;
            var pollFds = new pollfd[count];
            for (var i = 0; i < count; i++)
            {
                pollFds[i] = new pollfd
                {
                    fd = _deviceFds[i],
                    events = NativeLibc.POLLIN,
                    revents = 0
                };
            }

            int ret;
            fixed (pollfd* pFds = pollFds)
            {
                ret = NativeLibc.poll(pFds, (uint)count, 100);
            }

            if (ret <= 0) continue;

            for (var i = 0; i < count; i++)
            {
                if ((pollFds[i].revents & NativeLibc.POLLIN) == 0) continue;

                var fd = pollFds[i].fd;
                input_event ev;

                while (NativeLibc.read(fd, &ev, (nuint)sizeof(input_event)) == sizeof(input_event))
                {
                    ProcessEvent(ev);
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

    private void ReleaseAllGrabs()
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

    private void CloseAllDevices()
    {
        ReleaseAllGrabs();
        foreach (var fd in _deviceFds)
        {
            try { NativeLibc.close(fd); } catch { }
        }
        _deviceFds.Clear();
        _devicePaths.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();
        _virtualKeyboard.Dispose();
    }
}
