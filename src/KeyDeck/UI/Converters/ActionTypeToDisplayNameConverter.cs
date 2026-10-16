using System.Globalization;
using System.Windows.Data;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Converters;

/// <summary>
/// Converts KeyDeck enum values (ActionType, MediaCommand, SystemCommand, NumLockMacroMode, HudVisibilityMode, AppTheme)
/// into user-friendly UI display strings.
/// </summary>
public class ActionTypeToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ActionType actionType => actionType switch
            {
                ActionType.None          => "(None)",
                ActionType.LaunchApp     => "Launch Application",
                ActionType.OpenUrl       => "Open Webpage URL",
                ActionType.OpenFolder    => "Open Folder",
                ActionType.Hotkey        => "Keyboard Hotkey",
                ActionType.RunScript     => "Run Script (.ps1 / .bat)",
                ActionType.MediaControl  => "Media Playback Control",
                ActionType.SystemAction  => "Windows System Action",
                ActionType.TextPaste     => "Paste Text Snippet",
                ActionType.MultiAction   => "Multi-Action Sequence",
                ActionType.SwitchProfile => "Switch Profile",
                _                        => actionType.ToString()
            },
            MediaCommand mediaCmd => mediaCmd switch
            {
                MediaCommand.PlayPause  => "Play / Pause",
                MediaCommand.NextTrack  => "Next Track",
                MediaCommand.PrevTrack  => "Previous Track",
                MediaCommand.Stop       => "Stop Playback",
                MediaCommand.VolumeUp   => "Volume Up (Auto-Repeat)",
                MediaCommand.VolumeDown => "Volume Down (Auto-Repeat)",
                MediaCommand.VolumeMute => "Mute / Unmute",
                _                       => mediaCmd.ToString()
            },
            SystemCommand sysCmd => sysCmd switch
            {
                SystemCommand.LockWorkstation => "Lock Workstation (Win+L)",
                SystemCommand.Sleep           => "Sleep PC",
                SystemCommand.EmptyRecycleBin => "Empty Recycle Bin",
                SystemCommand.TakeScreenshot  => "Take Screenshot (Win+Shift+S)",
                SystemCommand.MinimizeAll     => "Show Desktop / Minimize All (Win+D)",
                _                             => sysCmd.ToString()
            },
            NumLockMacroMode numLockMode => numLockMode switch
            {
                NumLockMacroMode.OffMeansMacro => "NumLock OFF = Macros",
                NumLockMacroMode.OnMeansMacro  => "NumLock ON = Macros",
                _                              => numLockMode.ToString()
            },
            HudVisibilityMode hudMode => hudMode switch
            {
                HudVisibilityMode.AlwaysOnWhenNumLockOff => "Always On (When Active)",
                HudVisibilityMode.FadeOutOnPress         => "Fade Out After Press",
                HudVisibilityMode.Disabled               => "Disabled (Hidden)",
                _                                        => hudMode.ToString()
            },
            AppTheme theme => theme switch
            {
                AppTheme.Dark  => "Dark",
                AppTheme.Light => "Light",
                _              => theme.ToString()
            },
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
