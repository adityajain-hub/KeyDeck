namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Abstraction for monitoring the physical and logical state of the Num Lock key.
/// </summary>
public interface INumLockMonitor : IDisposable
{
    /// <summary>
    /// Gets whether Num Lock is currently toggled ON.
    /// </summary>
    bool IsNumLockOn { get; }

    /// <summary>
    /// Raised whenever the Num Lock hardware state transitions between ON and OFF.
    /// </summary>
    event Action<bool>? NumLockChanged;

    /// <summary>
    /// Forces an immediate evaluation and event broadcast of the hardware Num Lock state.
    /// </summary>
    void Refresh();
}
