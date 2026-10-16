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
/// <remarks>
/// <para>
/// This class is registered as a singleton and implements <see cref="IActionExecutor"/>
/// so consumers never depend on the concrete type.
/// </para>
/// <para>
/// <b>Stuck-key safety:</b> The hotkey path sends the press phase and release phase as two
/// separate <c>SendInput</c> calls with the release inside a <see langword="finally"/> block,
/// ensuring modifier keys (Ctrl, Alt, Shift, Win) are always released even if the target
/// application crashes between the two phases.
/// </para>
/// <para>
/// <b>URL security:</b> <c>OpenUrl</c> actions are validated to allow only <c>http://</c>
/// and <c>https://</c> schemes, blocking <c>javascript:</c>, <c>file:</c>, and similar vectors.
/// </para>
/// </remarks>
public sealed class ActionExecutor : IActionExecutor
{
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
                    ExecuteHotkey(action);
                    break;

                case ActionType.RunScript:
                    ExecuteRunScript(action);
                    break;

                case ActionType.MediaControl:
                    ExecuteMediaControl(action.MediaCommand);
                    break;

                case ActionType.SystemAction:
                    ExecuteSystemAction(action.SystemCommand);
                    break;

                case ActionType.TextPaste:
                    ExecuteTextPaste(action.TextToPaste);
                    break;

                case ActionType.MultiAction:
                    await ExecuteMultiActionAsync(action.SubActions).ConfigureAwait(false);
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

    /// <summary>
    /// Launches an application or opens a file via the Windows shell.
    /// Resolves the working directory from the executable path when possible.
    /// </summary>
    private static void ExecuteLaunchApp(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] LaunchApp: TargetPath is empty; no-op.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName       = action.TargetPath,
            Arguments      = action.Arguments ?? string.Empty,
            UseShellExecute = true
        };

        try
        {
            var dir = Path.GetDirectoryName(action.TargetPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                startInfo.WorkingDirectory = dir;
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — working directory is optional.
            Trace.WriteLine($"[ActionExecutor] LaunchApp: could not resolve working directory: {ex.Message}");
        }

        Process.Start(startInfo);
    }

    /// <summary>
    /// Opens a URL in the default browser.
    /// Prepends <c>https://</c> when no scheme is present and validates that only
    /// <c>http</c> or <c>https</c> schemes are forwarded to the shell.
    /// </summary>
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

        // Security: only http/https schemes are forwarded to the shell.
        // Blocks javascript:, file:, ftp:, and other injection vectors.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Trace.WriteLine($"[ActionExecutor] OpenUrl: blocked non-http(s) URL '{url}'.");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    /// <summary>
    /// Opens a folder in Windows Explorer.
    /// Expands any <c>%ENVIRONMENT%</c> variables in the path before resolving.
    /// </summary>
    private static void ExecuteOpenFolder(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] OpenFolder: TargetPath is empty; no-op.");
            return;
        }

        var folder = Environment.ExpandEnvironmentVariables(action.TargetPath);
        if (!Directory.Exists(folder))
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
    /// Synthesizes a key chord via <c>SendInput</c>.
    /// The press and release phases are sent as separate calls with the
    /// release inside a <see langword="finally"/> block, guaranteeing that
    /// modifier keys are always released even if an exception occurs mid-execution.
    /// </summary>
    private static void ExecuteHotkey(KeyAction action)
    {
        if (action.HotkeyVirtualKeys.Count == 0)
        {
            Trace.WriteLine("[ActionExecutor] Hotkey: no virtual keys defined; no-op.");
            return;
        }

        var keys = action.HotkeyVirtualKeys;

        // Build press-phase array
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

        // Build release-phase array (reverse order)
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

        // Press phase
        NativeMethods.SendInput((uint)pressInputs.Length, pressInputs, Marshal.SizeOf<NativeMethods.INPUT>());

        // Release phase is always executed — even if the application throws between
        // the press and release, modifier keys will not remain stuck.
        try { /* intentionally empty */ }
        finally
        {
            NativeMethods.SendInput((uint)releaseInputs.Length, releaseInputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }
    }

    // ── Script / RunScript ─────────────────────────────────────────────────

    /// <summary>
    /// Runs a script file. Supports <c>.ps1</c>, <c>.bat</c>, <c>.cmd</c>,
    /// and any other file type (opened via shell association).
    /// </summary>
    private static void ExecuteRunScript(KeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPath))
        {
            Trace.WriteLine("[ActionExecutor] RunScript: TargetPath is empty; no-op.");
            return;
        }

        var scriptPath = Environment.ExpandEnvironmentVariables(action.TargetPath);
        var ext        = Path.GetExtension(scriptPath).ToLowerInvariant();

        ProcessStartInfo startInfo = ext switch
        {
            ".ps1" => new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {action.Arguments}",
                UseShellExecute = false,
                CreateNoWindow  = true
            },
            ".bat" or ".cmd" => new ProcessStartInfo
            {
                FileName        = "cmd.exe",
                Arguments       = $"/c \"\"{scriptPath}\" {action.Arguments}\"",
                UseShellExecute = false,
                CreateNoWindow  = true
            },
            _ => new ProcessStartInfo
            {
                FileName        = scriptPath,
                Arguments       = action.Arguments ?? string.Empty,
                UseShellExecute = true
            }
        };

        Process.Start(startInfo);
    }

    // ── Media ──────────────────────────────────────────────────────────────

    /// <summary>Sends a single hardware-level media key event.</summary>
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

    /// <summary>
    /// Executes a Windows system command such as lock, sleep, screenshot, or minimize all.
    /// </summary>
    private static void ExecuteSystemAction(SystemCommand cmd)
    {
        switch (cmd)
        {
            case SystemCommand.LockWorkstation:
                NativeMethods.LockWorkStation();
                break;

            case SystemCommand.Sleep:
                NativeMethods.SetSuspendState(bHibernate: false, bForce: true, bWakeupEventsDisabled: true);
                break;

            case SystemCommand.EmptyRecycleBin:
                // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
                NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, dwFlags: 7u);
                break;

            case SystemCommand.TakeScreenshot:
                // Win + Shift + S → Windows Snipping Tool overlay
                ExecuteHotkey(new KeyAction
                {
                    HotkeyVirtualKeys = new List<int>
                    {
                        NativeMethods.VK_LWIN,
                        NativeMethods.VK_SHIFT,
                        0x53 // 'S'
                    }
                });
                break;

            case SystemCommand.MinimizeAll:
                // Win + D → toggle show desktop
                ExecuteHotkey(new KeyAction
                {
                    HotkeyVirtualKeys = new List<int>
                    {
                        NativeMethods.VK_LWIN,
                        0x44 // 'D'
                    }
                });
                break;

            default:
                Trace.WriteLine($"[ActionExecutor] SystemAction: unknown command '{cmd}'.");
                break;
        }
    }

    // ── Text paste ─────────────────────────────────────────────────────────

    /// <summary>
    /// Types <paramref name="text"/> into the focused window using Unicode
    /// <c>KEYEVENTF_UNICODE</c> input events. Newline characters are sent
    /// as <c>VK_RETURN</c> key events for maximum compatibility.
    /// </summary>
    private static void ExecuteTextPaste(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Trace.WriteLine("[ActionExecutor] TextPaste: text is null or empty; no-op.");
            return;
        }

        var inputs = new List<NativeMethods.INPUT>(capacity: text.Length * 2);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // Normalize Windows-style CRLF to a single newline
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++; // consume '\n'
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
        }

        var arr = inputs.ToArray();
        NativeMethods.SendInput((uint)arr.Length, arr, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ── MultiAction ────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a sequence of sub-actions in order, with an optional per-action delay.
    /// </summary>
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

    /// <summary>
    /// Sends a single key-down followed by key-up event pair.
    /// </summary>
    /// <param name="vk">Virtual key code.</param>
    /// <param name="isExtended">
    /// <see langword="true"/> for extended keys (media, Win, etc.).
    /// </param>
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
    /// Returns <see langword="true"/> for virtual keys that require the extended-key flag
    /// in their <c>KEYBDINPUT.dwFlags</c> field.
    /// </summary>
    private static bool IsExtendedKey(int vk) =>
        vk is NativeMethods.VK_LWIN
            or NativeMethods.VK_RWIN
            or NativeMethods.VK_MEDIA_PLAY_PAUSE
            or NativeMethods.VK_MEDIA_NEXT_TRACK
            or NativeMethods.VK_MEDIA_PREV_TRACK
            or NativeMethods.VK_MEDIA_STOP
            or NativeMethods.VK_VOLUME_UP
            or NativeMethods.VK_VOLUME_DOWN
            or NativeMethods.VK_VOLUME_MUTE;
}
