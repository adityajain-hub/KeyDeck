namespace KeyDeck.Infrastructure.Config;

/// <summary>
/// Central repository for all application-wide constants.
/// Replaces hard-coded literals scattered across the codebase so that
/// changing a value only requires editing one location.
/// </summary>
internal static class AppConstants
{
    // ── Application identity ────────────────────────────────────────────────
    /// <summary>Global mutex name used for single-instance enforcement.</summary>
    public const string SingleInstanceMutexName = "KeyDeck_SingleInstance_Mutex_2026";

    /// <summary>Global event name used to signal an already-running instance to restore and show its main window.</summary>
    public const string SingleInstanceEventName = "KeyDeck_ShowWindow_Event_2026";

    /// <summary>Human-readable application name used in tray tooltip, registry, and dialogs.</summary>
    public const string AppName = "KeyDeck";

    // ── File-system paths (relative to %APPDATA%) ──────────────────────────
    /// <summary>Root folder under %APPDATA% that KeyDeck owns.</summary>
    public const string AppDataFolderName = "KeyDeck";

    /// <summary>Sub-folder inside <see cref="AppDataFolderName"/> that stores profile JSON files.</summary>
    public const string ProfilesFolderName = "profiles";

    /// <summary>Sub-folder inside <see cref="AppDataFolderName"/> that stores cached icon PNGs.</summary>
    public const string IconsFolderName = "icons";

    /// <summary>File name of the global settings JSON file.</summary>
    public const string SettingsFileName = "settings.json";

    // ── Windows Registry ───────────────────────────────────────────────────
    /// <summary>HKCU registry sub-key path for Windows startup programs.</summary>
    public const string StartupRegistryKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Registry value name written under <see cref="StartupRegistryKeyPath"/>.</summary>
    public const string StartupRegistryValueName = AppName;

    // ── CLI arguments ──────────────────────────────────────────────────────
    /// <summary>Command-line argument that causes the app to start minimized to tray.</summary>
    public const string StartMinimizedArg = "--minimized";

    // ── HUD / tray ─────────────────────────────────────────────────────────
    /// <summary>Tray icon tooltip text.</summary>
    public const string TrayTooltipText = "KeyDeck \u2014 Numpad Macro Pad";

    // ── Timing ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Milliseconds to wait after a Num Lock WM_KEYUP before reading
    /// the hardware toggle state, allowing Windows to update <c>GetKeyState</c>.
    /// </summary>
    public const int NumLockSettleDelayMs = 20;

    /// <summary>Interval in milliseconds at which the Num Lock state is polled as a fallback.</summary>
    public const int NumLockPollIntervalMs = 500;
}
