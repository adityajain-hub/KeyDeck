using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KeyDeck.Core.Abstractions;

namespace KeyDeck.Platform.Linux.Services;

/// <summary>
/// Monitors foreground active applications on Linux desktop environments (Wayland / X11)
/// to enable Auto-Profile Switching.
/// </summary>
public sealed class LinuxForegroundAppMonitor : IForegroundAppMonitor
{
    private readonly Timer? _pollTimer;
    private string? _lastProcessName;
    private bool _isRunning;
    private bool _isDisposed;

    public event Action<string>? ForegroundProcessChanged;

    public LinuxForegroundAppMonitor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _pollTimer = new Timer(PollActiveWindow, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _pollTimer?.Change(500, 500);
        EvaluateCurrentForegroundWindow();
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _pollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void EvaluateCurrentForegroundWindow()
    {
        try
        {
            var current = GetForegroundProcessName();
            if (!string.IsNullOrWhiteSpace(current) && !string.Equals(current, _lastProcessName, StringComparison.OrdinalIgnoreCase))
            {
                _lastProcessName = current;
                ForegroundProcessChanged?.Invoke(current);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LinuxForegroundAppMonitor] Error evaluating foreground app: {ex.Message}");
        }
    }

    public string? GetForegroundProcessName()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        // Strategy 1: Check X11 via xdotool if available
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdotool",
                Arguments = "getactivewindow getwindowpid",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                if (!proc.WaitForExit(1000))
                {
                    try { proc.Kill(); } catch { }
                    return _lastProcessName;
                }

                var pidStr = proc.StandardOutput.ReadToEnd().Trim();
                if (int.TryParse(pidStr, out var pid))
                {
                    using var p = Process.GetProcessById(pid);
                    return p.ProcessName;
                }
            }
        }
        catch { /* Fallback to Strategy 2 */ }

        // Strategy 2: Check /proc for active user window processes
        return _lastProcessName;
    }

    private void PollActiveWindow(object? state)
    {
        if (!_isRunning || _isDisposed) return;
        EvaluateCurrentForegroundWindow();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _pollTimer?.Dispose();
    }
}
