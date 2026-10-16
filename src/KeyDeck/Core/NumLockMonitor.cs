using System.Timers;
using KeyDeck.Infrastructure.Config;
using KeyDeck.Platform.Win32;
using Timer = System.Timers.Timer;

namespace KeyDeck.Core;

/// <summary>
/// Monitors Windows Num Lock hardware state and broadcasts changes.
/// </summary>
public sealed class NumLockMonitor : IDisposable
{
    private readonly Timer _pollTimer;
    private bool _lastState;
    private bool _isDisposed;

    public bool IsNumLockOn => GetHardwareNumLockState();

    public event Action<bool>? NumLockChanged;

    public NumLockMonitor()
    {
        _lastState = IsNumLockOn;

        // Fallback polling in case Num Lock is toggled via on-screen keyboard or RDP
        // (the low-level hook fires for physical keyboards; polling catches everything else).
        _pollTimer = new Timer(AppConstants.NumLockPollIntervalMs);
        _pollTimer.Elapsed += OnTimerElapsed;
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Refresh();
    }

    /// <summary>
    /// Forces an immediate evaluation of the hardware Num Lock state.
    /// </summary>
    public void Refresh()
    {
        var current = GetHardwareNumLockState();
        if (current != _lastState)
        {
            _lastState = current;
            NumLockChanged?.Invoke(current);
        }
    }

    public static bool GetHardwareNumLockState()
    {
        // Low-order bit is 1 if key is toggled ON
        return (NativeMethods.GetKeyState(NativeMethods.VK_NUMLOCK) & 0x0001) != 0;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _isDisposed = true;
        }
    }
}
