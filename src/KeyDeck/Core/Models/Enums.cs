namespace KeyDeck.Core.Models;

/// <summary>
/// Identifies the 16 bindable keys on a standard physical numpad.
/// </summary>
public enum NumpadKeyId
{
    Numpad0 = 0,
    Numpad1 = 1,
    Numpad2 = 2,
    Numpad3 = 3,
    Numpad4 = 4,
    Numpad5 = 5,
    Numpad6 = 6,
    Numpad7 = 7,
    Numpad8 = 8,
    Numpad9 = 9,
    NumpadDecimal = 10,
    NumpadAdd = 11,
    NumpadSubtract = 12,
    NumpadMultiply = 13,
    NumpadDivide = 14,
    NumpadEnter = 15
}

/// <summary>
/// Supported macro action types.
/// </summary>
public enum ActionType
{
    None = 0,
    LaunchApp = 1,
    OpenUrl = 2,
    OpenFolder = 3,
    Hotkey = 4,
    RunScript = 5,
    MediaControl = 6,
    SystemAction = 7,
    TextPaste = 8,
    MultiAction = 9
}

/// <summary>
/// Media playback commands.
/// </summary>
public enum MediaCommand
{
    PlayPause = 0,
    NextTrack = 1,
    PrevTrack = 2,
    Stop = 3,
    VolumeUp = 4,
    VolumeDown = 5,
    VolumeMute = 6
}

/// <summary>
/// Windows system automation commands.
/// </summary>
public enum SystemCommand
{
    LockWorkstation = 0,
    Sleep = 1,
    EmptyRecycleBin = 2,
    TakeScreenshot = 3,
    MinimizeAll = 4
}

/// <summary>
/// On-screen HUD overlay visibility behaviors.
/// </summary>
public enum HudVisibilityMode
{
    AlwaysOnWhenNumLockOff = 0,
    FadeOutOnPress = 1,
    Disabled = 2
}

/// <summary>
/// Determines which Num Lock state activates macro mode.
/// </summary>
public enum NumLockMacroMode
{
    OffMeansMacro = 0,
    OnMeansMacro = 1
}

/// <summary>
/// Application visual theme.
/// </summary>
public enum AppTheme
{
    Dark = 0,
    Light = 1
}
