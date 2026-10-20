using System.Diagnostics;
using System.IO;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Platform.Linux.Native;
using KeyDeck.Platform.Linux.VirtualKeyboard;

namespace KeyDeck.Platform.Linux.Services;

/// <summary>
/// Executes macro actions on Linux systems using evdev/uinput, xdg-open, systemd loginctl, and desktop tools.
/// </summary>
public sealed class LinuxActionExecutor : IActionExecutor
{
    private readonly UInputVirtualKeyboard _virtualKeyboard;
    private readonly IProfileManager? _profileManager;
    private readonly IEnvironmentService _environmentService;

    public LinuxActionExecutor(
        UInputVirtualKeyboard? virtualKeyboard = null,
        IProfileManager? profileManager = null,
        IEnvironmentService? environmentService = null)
    {
        _virtualKeyboard = virtualKeyboard ?? new UInputVirtualKeyboard();
        _profileManager = profileManager;
        _environmentService = environmentService ?? KeyDeck.Infrastructure.Services.EnvironmentService.Instance;
    }

    public async Task ExecuteAsync(KeyAction? action)
    {
        if (action == null || action.Type == ActionType.None) return;

        try
        {
            switch (action.Type)
            {
                case ActionType.LaunchApp:
                    ExecuteLaunchApp(action);
                    break;

                case ActionType.OpenUrl:
                    ExecuteOpenUrl(action);
                    break;

                case ActionType.OpenFolder:
                    ExecuteOpenFolder(action);
                    break;

                case ActionType.Hotkey:
                    await ExecuteHotkeyAsync(action).ConfigureAwait(false);
                    break;

                case ActionType.RunScript:
                    ExecuteRunScript(action);
                    break;

                case ActionType.MediaControl:
                    await ExecuteMediaControlAsync(action.MediaCommand).ConfigureAwait(false);
                    break;

                case ActionType.SystemAction:
                    await ExecuteSystemActionAsync(action.SystemCommand).ConfigureAwait(false);
                    break;

                case ActionType.TextPaste:
                    await ExecuteTextPasteAsync(action.TextToPaste).ConfigureAwait(false);
                    break;

                case ActionType.MultiAction:
                    await ExecuteMultiActionAsync(action.SubActions).ConfigureAwait(false);
                    break;

                case ActionType.SwitchProfile:
                    ExecuteSwitchProfile(action);
                    break;

                default:
                    Trace.WriteLine($"[LinuxActionExecutor] Unsupported action type: {action.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LinuxActionExecutor] Execution error for {action.Type}: {ex.Message}");
        }
    }

    private void ExecuteLaunchApp(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath)) return;

        var target = _environmentService.ExpandEnvironmentVariables(action.TargetPath.Trim());
        var psi = new ProcessStartInfo
        {
            FileName = target,
            Arguments = action.Arguments ?? string.Empty,
            UseShellExecute = true
        };

        try
        {
            Process.Start(psi);
        }
        catch
        {
            // Fallback via xdg-open or gtk-launch
            Process.Start("xdg-open", $"\"{target}\"");
        }
    }

    private static void ExecuteOpenUrl(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath)) return;

        var url = action.TargetPath.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = $"\"{url}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private void ExecuteOpenFolder(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath)) return;

        var folder = _environmentService.ExpandEnvironmentVariables(action.TargetPath.Trim());
        Process.Start(new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = $"\"{folder}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private async Task ExecuteHotkeyAsync(KeyAction action)
    {
        if (action.HotkeyVirtualKeys.Count == 0) return;

        var linuxCodes = new List<ushort>(action.HotkeyVirtualKeys.Count);
        foreach (var vk in action.HotkeyVirtualKeys)
        {
            var code = LinuxInputCodes.MapWinVkToLinuxKey(vk);
            if (code == 0)
            {
                Trace.WriteLine($"[LinuxActionExecutor] Unmapped VK 0x{vk:X2}; hotkey skipped to prevent incomplete keystrokes.");
                return;
            }
            linuxCodes.Add(code);
        }

        if (linuxCodes.Count > 0)
        {
            await _virtualKeyboard.SendChordAsync(linuxCodes).ConfigureAwait(false);
        }
    }

    private void ExecuteRunScript(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath)) return;

        var path = _environmentService.ExpandEnvironmentVariables(action.TargetPath.Trim());
        var ext = Path.GetExtension(path).ToLowerInvariant();

        ProcessStartInfo psi = ext switch
        {
            ".py" => new ProcessStartInfo("python3", $"\"{path}\" {action.Arguments}"),
            _     => new ProcessStartInfo("/bin/bash", $"\"{path}\" {action.Arguments}")
        };

        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        Process.Start(psi);
    }

    private async Task ExecuteMediaControlAsync(MediaCommand cmd)
    {
        ushort code = cmd switch
        {
            MediaCommand.PlayPause  => LinuxInputCodes.KEY_PLAYPAUSE,
            MediaCommand.NextTrack  => LinuxInputCodes.KEY_NEXTSONG,
            MediaCommand.PrevTrack  => LinuxInputCodes.KEY_PREVIOUSSONG,
            MediaCommand.Stop       => LinuxInputCodes.KEY_STOPCD,
            MediaCommand.VolumeUp   => LinuxInputCodes.KEY_VOLUMEUP,
            MediaCommand.VolumeDown => LinuxInputCodes.KEY_VOLUMEDOWN,
            MediaCommand.VolumeMute => LinuxInputCodes.KEY_MUTE,
            _                       => 0
        };

        if (code != 0)
        {
            await _virtualKeyboard.SendKeyStrokeAsync(code).ConfigureAwait(false);
        }
    }

    private async Task ExecuteSystemActionAsync(SystemCommand cmd)
    {
        switch (cmd)
        {
            case SystemCommand.LockWorkstation:
                Process.Start(new ProcessStartInfo("loginctl", "lock-session") { UseShellExecute = false, CreateNoWindow = true });
                break;

            case SystemCommand.Sleep:
                Process.Start(new ProcessStartInfo("systemctl", "suspend") { UseShellExecute = false, CreateNoWindow = true });
                break;

            case SystemCommand.EmptyRecycleBin:
                var trashPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash");
                if (Directory.Exists(trashPath))
                {
                    try
                    {
                        var filesDir = Path.Combine(trashPath, "files");
                        if (Directory.Exists(filesDir)) Directory.Delete(filesDir, true);
                        var infoDir = Path.Combine(trashPath, "info");
                        if (Directory.Exists(infoDir)) Directory.Delete(infoDir, true);
                    }
                    catch { /* non-critical */ }
                }
                break;

            case SystemCommand.TakeScreenshot:
                // Emit PrintScreen (KEY_SYSRQ = 99)
                await _virtualKeyboard.SendKeyStrokeAsync(LinuxInputCodes.KEY_SYSRQ).ConfigureAwait(false);
                break;

            case SystemCommand.MinimizeAll:
                // Super + D minimizes all on standard Linux desktops (GNOME, KDE, XFCE)
                await _virtualKeyboard.SendChordAsync(new[] { LinuxInputCodes.KEY_LEFTMETA, LinuxInputCodes.KEY_D }).ConfigureAwait(false);
                break;
        }
    }

    private async Task ExecuteTextPasteAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Try wl-copy (Wayland) or xclip (X11) clipboard injection then simulate Ctrl+Shift+V / Ctrl+V
        try
        {
            var wlCopy = Process.Start(new ProcessStartInfo
            {
                FileName = "wl-copy",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (wlCopy != null)
            {
                await wlCopy.StandardInput.WriteAsync(text).ConfigureAwait(false);
                wlCopy.StandardInput.Close();
                await wlCopy.WaitForExitAsync().ConfigureAwait(false);

                if (wlCopy.ExitCode == 0)
                {
                    // Simulate Ctrl + V
                    await _virtualKeyboard.SendChordAsync(new[] { LinuxInputCodes.KEY_LEFTCTRL, LinuxInputCodes.KEY_V }).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch { /* Fallback to xclip or direct typing */ }

        try
        {
            var xclip = Process.Start(new ProcessStartInfo
            {
                FileName = "xclip",
                Arguments = "-selection clipboard",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (xclip != null)
            {
                await xclip.StandardInput.WriteAsync(text).ConfigureAwait(false);
                xclip.StandardInput.Close();
                await xclip.WaitForExitAsync().ConfigureAwait(false);

                // Simulate Ctrl + V
                await _virtualKeyboard.SendChordAsync(new[] { LinuxInputCodes.KEY_LEFTCTRL, LinuxInputCodes.KEY_V }).ConfigureAwait(false);
                return;
            }
        }
        catch { /* fallback */ }
    }

    private void ExecuteSwitchProfile(KeyAction action)
    {
        var profileId = !string.IsNullOrWhiteSpace(action.TargetProfileId)
            ? action.TargetProfileId
            : action.TargetPath;

        if (!string.IsNullOrWhiteSpace(profileId) && _profileManager != null)
        {
            _profileManager.SetActiveProfile(profileId);
        }
    }

    private async Task ExecuteMultiActionAsync(List<KeyAction> subActions)
    {
        foreach (var sub in subActions)
        {
            await ExecuteAsync(sub).ConfigureAwait(false);
            if (sub.DelayMs > 0)
            {
                await Task.Delay(sub.DelayMs).ConfigureAwait(false);
            }
        }
    }
}
