using System.Text;
using System.Windows.Input;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Infrastructure;

/// <summary>
/// Utility helper for recording physical keyboard combinations and converting them into
/// Win32 virtual key codes and human-readable display strings.
/// </summary>
public static class HotkeyRecorder
{
    /// <summary>
    /// Processes a WPF <see cref="KeyEventArgs"/> from a key down event, inspecting active modifier
    /// keys and standard virtual key conversions.
    /// </summary>
    /// <param name="e">The WPF keyboard event arguments.</param>
    /// <returns>
    /// A tuple containing the list of Win32 virtual key codes and formatted display text,
    /// or <see langword="null"/> if only a solitary modifier was pressed.
    /// </returns>
    public static (List<int> virtualKeys, string displayText)? ProcessKeyDown(KeyEventArgs e)
    {
        if (e == null)
        {
            return null;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Skip lone modifier presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return null;
        }

        var virtualKeys = new List<int>();
        var sb = new StringBuilder();

        // Modifiers
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            virtualKeys.Add(NativeMethods.VK_CONTROL);
            sb.Append("Ctrl + ");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            virtualKeys.Add(NativeMethods.VK_SHIFT);
            sb.Append("Shift + ");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            virtualKeys.Add(NativeMethods.VK_MENU);
            sb.Append("Alt + ");
        }

        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
        {
            virtualKeys.Add(NativeMethods.VK_LWIN);
            sb.Append("Win + ");
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk != 0 && !virtualKeys.Contains(vk))
        {
            virtualKeys.Add(vk);
            sb.Append(FormatKeyName(key, vk));
        }

        return (virtualKeys, sb.ToString());
    }

    /// <summary>
    /// Formats common keyboard keys into friendly names for UI display.
    /// </summary>
    private static string FormatKeyName(Key key, int vk)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Escape => "Esc",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => char.IsLetterOrDigit((char)vk) ? ((char)vk).ToString().ToUpperInvariant() : key.ToString()
        };
    }
}
