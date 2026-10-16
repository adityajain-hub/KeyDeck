using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Core;

/// <summary>
/// Executes all 9 macro action types asynchronously without blocking the keyboard hook thread.
/// </summary>
public sealed class ActionExecutor : IActionExecutor
{
    public IProfileManager? ProfileManager { get; set; }
    private readonly KeyDeck.Core.Abstractions.IEnvironmentService _environmentService;

    public ActionExecutor(IProfileManager? profileManager = null, KeyDeck.Core.Abstractions.IEnvironmentService? environmentService = null)
    {
        ProfileManager = profileManager;
        _environmentService = environmentService ?? KeyDeck.Infrastructure.Services.EnvironmentService.Instance;
    }
    private const int HotkeyHoldDelayMs = 20;
    private const int TextPasteChunkSize = 64;
    private const int TextPasteChunkDelayMs = 5;

    // ── IActionExecutor ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ExecuteAsync(KeyAction? action)
    {
        if (action == null || action.Type == ActionType.None)
        {
            return;
        }

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
                    ExecuteMediaControl(action.MediaCommand);
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
                    Trace.WriteLine($"[ActionExecutor] Unknown action type: {action.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ActionExecutor] ExecuteAsync failed for action type '{action.Type}': {ex.Message}");
        }
    }

    // ── Launch / Open ──────────────────────────────────────────────────────

    private void ExecuteLaunchApp(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] LaunchApp: TargetPath is empty; no-op.");
            return;
        }

        var expandedPath = _environmentService.ExpandEnvironmentVariables(action.TargetPath.Trim());
        var startInfo = new ProcessStartInfo
        {
            FileName        = expandedPath,
            Arguments       = action.Arguments ?? string.Empty,
            UseShellExecute = true
        };

        try
        {
            var dir = Path.GetDirectoryName(expandedPath);
            if (!string.IsNullOrEmpty(dir) && _environmentService.DirectoryExists(dir))
            {
                startInfo.WorkingDirectory = dir;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ActionExecutor] LaunchApp: could not resolve working directory: {ex.Message}");
        }

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ActionExecutor] LaunchApp direct start failed ({ex.Message}), trying ShellExecute fallback...");
            var ret = NativeMethods.ShellExecute(IntPtr.Zero, "open", expandedPath, action.Arguments, startInfo.WorkingDirectory, 1);
            if ((long)ret <= 32)
            {
                throw;
            }
        }
    }

    private static void ExecuteOpenUrl(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] OpenUrl: TargetPath is empty; no-op.");
            return;
        }

        var url = action.TargetPath.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Trace.WriteLine($"[ActionExecutor] OpenUrl: blocked non-http(s) URL '{url}'.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ActionExecutor] OpenUrl Process.Start failed ({ex.Message}), trying ShellExecute fallback...");
            var ret = NativeMethods.ShellExecute(IntPtr.Zero, "open", uri.AbsoluteUri, null, null, 1);
            if ((long)ret <= 32)
            {
                var escapedUrl = uri.AbsoluteUri.Replace("&", "^&");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{escapedUrl}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }
    }

    private void ExecuteOpenFolder(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] OpenFolder: TargetPath is empty; no-op.");
            return;
        }

        var folder = _environmentService.ExpandEnvironmentVariables(action.TargetPath.Trim());
        if (!_environmentService.DirectoryExists(folder))
        {
            Trace.WriteLine($"[ActionExecutor] OpenFolder: directory does not exist: '{folder}'.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName        = "explorer.exe",
            Arguments       = $"\"{folder}\"",
            UseShellExecute = true
        });
    }

    // ── Hotkey ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Synthesizes a key chord via <c>SendInput</c> with a brief hold delay so games,
    /// Electron apps, and global hotkey hooks reliably register the chord before release.
    /// </summary>
    private static async Task ExecuteHotkeyAsync(KeyAction action)
    {
        if (action.HotkeyVirtualKeys.Count == 0)
        {
            Trace.WriteLine("[ActionExecutor] Hotkey: no virtual keys defined; no-op.");
            return;
        }

        var keys = action.HotkeyVirtualKeys;
        var inputSize = Marshal.SizeOf<NativeMethods.INPUT>();

        var pressInputs = new NativeMethods.INPUT[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            var vk = keys[i];
            pressInputs[i] = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
            pressInputs[i].u.ki = new NativeMethods.KEYBDINPUT
            {
                wVk     = (ushort)vk,
                wScan   = (ushort)NativeMethods.MapVirtualKey((uint)vk, 0),
                dwFlags = IsExtendedKey(vk) ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0
            };
        }

        var releaseInputs = new NativeMethods.INPUT[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            var vk = keys[keys.Count - 1 - i];
            releaseInputs[i] = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
            releaseInputs[i].u.ki = new NativeMethods.KEYBDINPUT
            {
                wVk     = (ushort)vk,
                wScan   = (ushort)NativeMethods.MapVirtualKey((uint)vk, 0),
                dwFlags = NativeMethods.KEYEVENTF_KEYUP | (IsExtendedKey(vk) ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0)
            };
        }

        try
        {
            NativeMethods.SendInput((uint)pressInputs.Length, pressInputs, inputSize);
            await Task.Delay(HotkeyHoldDelayMs).ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.SendInput((uint)releaseInputs.Length, releaseInputs, inputSize);
        }
    }

    // ── Script / RunScript ─────────────────────────────────────────────────

    private void ExecuteRunScript(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] RunScript: TargetPath is empty; no-op.");
            return;
        }

        var scriptPath = _environmentService.ExpandEnvironmentVariables(action.TargetPath.Trim());
        var ext        = Path.GetExtension(scriptPath).ToLowerInvariant();
        var scriptDir  = Path.GetDirectoryName(scriptPath) ?? string.Empty;

        ProcessStartInfo startInfo = ext switch
        {
            ".ps1" => new ProcessStartInfo
            {
                FileName         = "powershell.exe",
                Arguments        = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {action.Arguments}",
                WorkingDirectory = _environmentService.DirectoryExists(scriptDir) ? scriptDir : string.Empty,
                UseShellExecute  = false,
                CreateNoWindow   = true
            },
            ".bat" or ".cmd" => new ProcessStartInfo
            {
                FileName         = "cmd.exe",
                Arguments        = $"/c \"\"{scriptPath}\" {action.Arguments}\"",
                WorkingDirectory = _environmentService.DirectoryExists(scriptDir) ? scriptDir : string.Empty,
                UseShellExecute  = false,
                CreateNoWindow   = true
            },
            _ => new ProcessStartInfo
            {
                FileName         = scriptPath,
                Arguments        = action.Arguments ?? string.Empty,
                WorkingDirectory = _environmentService.DirectoryExists(scriptDir) ? scriptDir : string.Empty,
                UseShellExecute  = true
            }
        };

        Process.Start(startInfo);
    }

    // ── Media ──────────────────────────────────────────────────────────────

    private static void ExecuteMediaControl(MediaCommand cmd)
    {
        int vk = cmd switch
        {
            MediaCommand.PlayPause  => NativeMethods.VK_MEDIA_PLAY_PAUSE,
            MediaCommand.NextTrack  => NativeMethods.VK_MEDIA_NEXT_TRACK,
            MediaCommand.PrevTrack  => NativeMethods.VK_MEDIA_PREV_TRACK,
            MediaCommand.Stop       => NativeMethods.VK_MEDIA_STOP,
            MediaCommand.VolumeUp   => NativeMethods.VK_VOLUME_UP,
            MediaCommand.VolumeDown => NativeMethods.VK_VOLUME_DOWN,
            MediaCommand.VolumeMute => NativeMethods.VK_VOLUME_MUTE,
            _                       => 0
        };

        if (vk != 0)
        {
            SendSingleKey(vk, isExtended: true);
        }
        else
        {
            Trace.WriteLine($"[ActionExecutor] MediaControl: unknown command '{cmd}'.");
        }
    }

    // ── System actions ─────────────────────────────────────────────────────

    private static async Task ExecuteSystemActionAsync(SystemCommand cmd)
    {
        switch (cmd)
        {
            case SystemCommand.LockWorkstation:
                NativeMethods.LockWorkStation();
                break;

            case SystemCommand.Sleep:
                // bForce: false allows applications to respond to suspend queries cleanly;
                // bWakeupEventsDisabled: false preserves user wake timers and alarms.
                NativeMethods.SetSuspendState(bHibernate: false, bForce: false, bWakeupEventsDisabled: false);
                break;

            case SystemCommand.EmptyRecycleBin:
                // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
                NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, dwFlags: 7u);
                break;

            case SystemCommand.TakeScreenshot:
                await ExecuteHotkeyAsync(new KeyAction
                {
                    HotkeyVirtualKeys = new List<int>
                    {
                        NativeMethods.VK_LWIN,
                        NativeMethods.VK_SHIFT,
                        0x53 // 'S'
                    }
                }).ConfigureAwait(false);
                break;

            case SystemCommand.MinimizeAll:
                await ExecuteHotkeyAsync(new KeyAction
                {
                    HotkeyVirtualKeys = new List<int>
                    {
                        NativeMethods.VK_LWIN,
                        0x44 // 'D'
                    }
                }).ConfigureAwait(false);
                break;

            default:
                Trace.WriteLine($"[ActionExecutor] SystemAction: unknown command '{cmd}'.");
                break;
        }
    }

    // ── Text paste ─────────────────────────────────────────────────────────

    /// <summary>
    /// Emits <paramref name="text"/> into the focused window using <c>KEYEVENTF_UNICODE</c>
    /// in bounded chunks so target window input queues do not overflow on large snippets.
    /// </summary>
    private static async Task ExecuteTextPasteAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Trace.WriteLine("[ActionExecutor] TextPaste: text is null or empty; no-op.");
            return;
        }

        var inputs = new List<NativeMethods.INPUT>(capacity: Math.Min(text.Length * 2, TextPasteChunkSize * 2));
        var inputSize = Marshal.SizeOf<NativeMethods.INPUT>();

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // Normalize CRLF to a single newline
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                c = '\n';
            }

            if (c == '\n')
            {
                var down = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
                down.u.ki = new NativeMethods.KEYBDINPUT { wVk = (ushort)NativeMethods.VK_RETURN };
                var up = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
                up.u.ki = new NativeMethods.KEYBDINPUT { wVk = (ushort)NativeMethods.VK_RETURN, dwFlags = NativeMethods.KEYEVENTF_KEYUP };
                inputs.Add(down);
                inputs.Add(up);
            }
            else
            {
                var down = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
                down.u.ki = new NativeMethods.KEYBDINPUT { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE };
                var up = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
                up.u.ki = new NativeMethods.KEYBDINPUT { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP };
                inputs.Add(down);
                inputs.Add(up);
            }

            if (inputs.Count >= TextPasteChunkSize * 2)
            {
                var chunk = inputs.ToArray();
                NativeMethods.SendInput((uint)chunk.Length, chunk, inputSize);
                inputs.Clear();
                await Task.Delay(TextPasteChunkDelayMs).ConfigureAwait(false);
            }
        }

        if (inputs.Count > 0)
        {
            var remaining = inputs.ToArray();
            NativeMethods.SendInput((uint)remaining.Length, remaining, inputSize);
        }
    }

    // ── SwitchProfile ──────────────────────────────────────────────────────

    private void ExecuteSwitchProfile(KeyAction action)
    {
        var profileId = !string.IsNullOrWhiteSpace(action.TargetProfileId)
            ? action.TargetProfileId
            : action.TargetPath;

        if (string.IsNullOrWhiteSpace(profileId))
        {
            Trace.WriteLine("[ActionExecutor] SwitchProfile: TargetProfileId is empty; no-op.");
            return;
        }

        if (ProfileManager == null)
        {
            Trace.WriteLine("[ActionExecutor] SwitchProfile: ProfileManager is not attached.");
            return;
        }

        ProfileManager.SetActiveProfile(profileId);
    }

    // ── MultiAction ────────────────────────────────────────────────────────

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

    // ── Shared helpers ─────────────────────────────────────────────────────

    private static void SendSingleKey(int vk, bool isExtended = false)
    {
        var flags  = isExtended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u;
        var inputs = new NativeMethods.INPUT[2];

        inputs[0] = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
        inputs[0].u.ki = new NativeMethods.KEYBDINPUT
        {
            wVk     = (ushort)vk,
            wScan   = (ushort)NativeMethods.MapVirtualKey((uint)vk, 0),
            dwFlags = flags
        };

        inputs[1] = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
        inputs[1].u.ki = new NativeMethods.KEYBDINPUT
        {
            wVk     = (ushort)vk,
            wScan   = (ushort)NativeMethods.MapVirtualKey((uint)vk, 0),
            dwFlags = NativeMethods.KEYEVENTF_KEYUP | flags
        };

        NativeMethods.SendInput(2, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>
    /// Returns <see langword="true"/> for virtual keys that require the <c>KEYEVENTF_EXTENDEDKEY</c>
    /// flag in <c>KEYBDINPUT.dwFlags</c> so Win32 apps and games distinguish them from numpad keys.
    /// </summary>
    public static bool IsExtendedKey(int vk) =>
        vk is NativeMethods.VK_LWIN
            or NativeMethods.VK_RWIN
            or NativeMethods.VK_RCONTROL
            or NativeMethods.VK_RMENU
            or NativeMethods.VK_INSERT
            or NativeMethods.VK_DELETE
            or NativeMethods.VK_HOME
            or NativeMethods.VK_END
            or NativeMethods.VK_PRIOR
            or NativeMethods.VK_NEXT
            or NativeMethods.VK_LEFT
            or NativeMethods.VK_UP
            or NativeMethods.VK_RIGHT
            or NativeMethods.VK_DOWN
            or NativeMethods.VK_SNAPSHOT
            or NativeMethods.VK_DIVIDE
            or NativeMethods.VK_CANCEL
            or NativeMethods.VK_NUMLOCK
            or NativeMethods.VK_MEDIA_PLAY_PAUSE
            or NativeMethods.VK_MEDIA_NEXT_TRACK
            or NativeMethods.VK_MEDIA_PREV_TRACK
            or NativeMethods.VK_MEDIA_STOP
            or NativeMethods.VK_VOLUME_UP
            or NativeMethods.VK_VOLUME_DOWN
            or NativeMethods.VK_VOLUME_MUTE;
}
