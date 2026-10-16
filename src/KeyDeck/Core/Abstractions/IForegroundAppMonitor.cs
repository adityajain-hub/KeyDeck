namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Abstraction for monitoring Windows foreground window changes.
/// </summary>
public interface IForegroundAppMonitor : IDisposable
{
    /// <summary>
    /// Raised when a different application enters the foreground while auto-switching is enabled.
    /// Carries the normalized executable name (e.g. <c>Code.exe</c>).
    /// </summary>
    event Action<string>? ForegroundProcessChanged;

    /// <summary>
    /// Installs the zero-polling OS WinEvent hook.
    /// </summary>
    void Start();

    /// <summary>
    /// Unhooks the OS WinEvent hook.
    /// </summary>
    void Stop();

    /// <summary>
    /// Evaluates the current foreground window immediately.
    /// </summary>
    void EvaluateCurrentForegroundWindow();
}
