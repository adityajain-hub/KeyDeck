using KeyDeck.Core.Models;

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Identifies which slice of application state changed.
/// </summary>
public enum AppStateProperty
{
    All,
    MacroInterception,
    AutoProfileSwitch,
    HudVisibilityMode,
    HudOpacity,
    HudLockedState,
    HudPosition,
    Theme,
    ActiveProfile
}

/// <summary>
/// Event arguments emitted when an application state property is mutated.
/// </summary>
public sealed class AppStateChangedEventArgs : EventArgs
{
    public AppStateProperty Property { get; }
    public object? NewValue { get; }

    public AppStateChangedEventArgs(AppStateProperty property, object? newValue = null)
    {
        Property = property;
        NewValue = newValue;
    }
}

/// <summary>
/// Centralized reactive state management service for KeyDeck.
/// Acts as the single source of truth for runtime application state and settings,
/// orchestrating state synchronization across MainWindow, System Tray, and HUD Overlay.
/// </summary>
public interface IAppStateService
{
    /// <summary>
    /// Gets the current application settings snapshot.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Gets whether physical numpad macro interception is globally active.
    /// </summary>
    bool IsMacroEnabled { get; }

    /// <summary>
    /// Gets whether foreground window auto-profile switching is active.
    /// </summary>
    bool IsAutoProfileSwitchEnabled { get; }

    /// <summary>
    /// Gets the current HUD overlay visibility mode.
    /// </summary>
    HudVisibilityMode HudMode { get; }

    /// <summary>
    /// Gets the current HUD overlay opacity (clamped between 0.20 and 1.00).
    /// </summary>
    double HudOpacity { get; }

    /// <summary>
    /// Gets whether the HUD overlay is locked in place with click-through enabled.
    /// </summary>
    bool IsHudLocked { get; }

    /// <summary>
    /// Gets the current active UI theme.
    /// </summary>
    AppTheme Theme { get; }

    /// <summary>
    /// Gets the currently active profile.
    /// </summary>
    Profile ActiveProfile { get; }

    /// <summary>
    /// Raised whenever any application state property changes.
    /// </summary>
    event Action<AppStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Raised when a request to snap the HUD back to bottom-right is initiated.
    /// </summary>
    event Action? ResetHudPositionRequested;

    void SetMacroEnabled(bool enabled);
    void SetAutoProfileSwitchEnabled(bool enabled);
    void SetHudMode(HudVisibilityMode mode);
    void SetHudOpacity(double opacity);
    void SetHudLocked(bool isLocked);
    void SetHudPosition(double left, double top);
    void SetTheme(AppTheme theme);
    void SetActiveProfile(string profileId);
    void RequestResetHudPosition();
}
