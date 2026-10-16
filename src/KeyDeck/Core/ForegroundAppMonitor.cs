using System.Diagnostics;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure.Services;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Core;

/// <summary>
/// Listens for OS foreground window changes via <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c>
/// and raises an event with the active process executable name when Per-App Auto-Profile Switching is enabled.
/// </summary>
public sealed class ForegroundAppMonitor : IForegroundAppMonitor
{
    private readonly AppSettings _settings;
    private readonly NativeMethods.WinEventDelegate _winEventProc;
    private readonly int _ownProcessId;
    private IntPtr _hookHandle = IntPtr.Zero;
    private string _lastProcessName = string.Empty;
    private bool _isDisposed;

    /// <summary>
    /// Fired when the foreground application changes while <see cref="AppSettings.IsAutoProfileSwitchEnabled"/> is true.
    /// Carries the executable file name (e.g., <c>Code.exe</c>).
    /// </summary>
    public event Action<string>? ForegroundProcessChanged;

    public ForegroundAppMonitor(AppSettings settings, IEnvironmentService? environmentService = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _ownProcessId = (environmentService ?? EnvironmentService.Instance).CurrentProcessId;

        // Pin delegate to field so GC never collects the unmanaged callback pointer
        _winEventProc = OnWinEventCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventProc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (_hookHandle == IntPtr.Zero)
        {
            Trace.WriteLine("[ForegroundAppMonitor] Failed to install SetWinEventHook.");
        }
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Evaluates a specific window handle and emits <see cref="ForegroundProcessChanged"/> if appropriate.
    /// </summary>
    private void OnWinEventCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (!_settings.IsAutoProfileSwitchEnabled || hwnd == IntPtr.Zero)
        {
            return;
        }

        InspectWindowHandle(hwnd);
    }

    /// <summary>
    /// Inspects the current foreground window immediately (useful when the user toggles the feature on).
    /// </summary>
    public void EvaluateCurrentForegroundWindow()
    {
        if (!_settings.IsAutoProfileSwitchEnabled)
        {
            return;
        }

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            InspectWindowHandle(hwnd);
        }
    }

    private void InspectWindowHandle(IntPtr hwnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0 || pid == _ownProcessId)
            {
                return;
            }

            using var process = Process.GetProcessById((int)pid);
            var procName = process.ProcessName;
            if (string.IsNullOrWhiteSpace(procName))
            {
                return;
            }

            var normalizedExe = procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? procName
                : procName + ".exe";

            if (!string.Equals(_lastProcessName, normalizedExe, StringComparison.OrdinalIgnoreCase))
            {
                _lastProcessName = normalizedExe;
                ForegroundProcessChanged?.Invoke(normalizedExe);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ForegroundAppMonitor] Could not resolve process for HWND {hwnd}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _isDisposed = true;
            GC.KeepAlive(_winEventProc);
        }
    }
}
