using KeyDeck.Core.Models;

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Abstraction for coordinating low-level keyboard hooking and macro interception.
/// </summary>
public interface IKeyInterceptor : IDisposable
{
    /// <summary>
    /// Gets or sets whether macro interception is temporarily bypassed (e.g. while recording hotkeys).
    /// </summary>
    bool IsTemporaryBypassEnabled { get; set; }

    /// <summary>
    /// Optional predicate evaluated on held keys to determine if typematic auto-repeat
    /// should re-trigger <see cref="NumpadKeyPressed"/> (e.g. for Volume controls).
    /// </summary>
    Func<NumpadKeyId, bool>? AllowKeyRepeatPredicate { get; set; }

    /// <summary>
    /// Gets whether macro mode is currently active based on Num Lock and macro settings.
    /// </summary>
    bool IsMacroModeActive { get; }

    /// <summary>
    /// Raised when an intercepted numpad key transition occurs.
    /// </summary>
    event Action<NumpadKeyId>? NumpadKeyPressed;

    /// <summary>
    /// Raised when an intercepted numpad key is released.
    /// </summary>
    event Action<NumpadKeyId>? NumpadKeyReleased;

    /// <summary>
    /// Raised when macro mode active state transitions between true and false.
    /// </summary>
    event Action<bool>? MacroModeStateChanged;

    void Start();
    void Stop();
    void NotifyMacroState();
}
