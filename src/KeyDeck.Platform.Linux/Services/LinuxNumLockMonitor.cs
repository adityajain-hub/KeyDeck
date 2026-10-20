using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KeyDeck.Core.Abstractions;

namespace KeyDeck.Platform.Linux.Services;

/// <summary>
/// Monitors Num Lock state on Linux by inspecting LED sysfs entries (/sys/class/leds/*::numlock/brightness).
/// Provides event-driven notification with fallback periodic polling.
/// </summary>
public sealed class LinuxNumLockMonitor : INumLockMonitor, IDisposable
{
    private const int DefaultPollIntervalMs = 500;
    private readonly Timer? _pollTimer;
    private bool _lastState;
    private bool _isDisposed;

    public bool IsNumLockOn => ReadCurrentNumLockState();

    public event Action<bool>? NumLockChanged;

    public LinuxNumLockMonitor(int pollIntervalMs = DefaultPollIntervalMs)
    {
        _lastState = ReadCurrentNumLockState();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _pollTimer = new Timer(OnPollTick, null, pollIntervalMs, pollIntervalMs);
        }
    }

    public void Refresh()
    {
        var current = ReadCurrentNumLockState();
        _lastState = current;
        NumLockChanged?.Invoke(current);
    }

    private void OnPollTick(object? state)
    {
        if (_isDisposed) return;

        var current = ReadCurrentNumLockState();
        if (current != _lastState)
        {
            _lastState = current;
            Trace.WriteLine($"[LinuxNumLockMonitor] NumLock state toggled -> {current}");
            NumLockChanged?.Invoke(current);
        }
    }

    private static bool ReadCurrentNumLockState()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        try
        {
            const string ledsBase = "/sys/class/leds";
            if (!Directory.Exists(ledsBase)) return false;

            var ledDirs = Directory.GetDirectories(ledsBase, "*numlock*");
            foreach (var dir in ledDirs)
            {
                var brightnessPath = Path.Combine(dir, "brightness");
                if (File.Exists(brightnessPath))
                {
                    var text = File.ReadAllText(brightnessPath).Trim();
                    if (int.TryParse(text, out var val) && val > 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LinuxNumLockMonitor] Failed to read sysfs LED state: {ex.Message}");
        }

        return false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _pollTimer?.Dispose();
    }
}
