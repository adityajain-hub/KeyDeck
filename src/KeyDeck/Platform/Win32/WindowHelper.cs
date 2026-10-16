using System.Windows;
using System.Windows.Interop;

namespace KeyDeck.Platform.Win32;

/// <summary>
/// Configures Win32 extended window styles for transparent, click-through or draggable overlays.
/// </summary>
public static class WindowHelper
{
    public static void MakeClickThrough(Window window) => SetClickThrough(window, true);

    public static void SetClickThrough(Window window, bool isClickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var currentExStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

        // Always keep WS_EX_LAYERED and WS_EX_TOOLWINDOW (hides HUD from Alt+Tab switcher)
        var baseStyle = currentExStyle |
                        NativeMethods.WS_EX_LAYERED |
                        NativeMethods.WS_EX_TOOLWINDOW |
                        NativeMethods.WS_EX_NOACTIVATE;

        long newExStyle = isClickThrough
            ? (baseStyle | NativeMethods.WS_EX_TRANSPARENT)
            : (baseStyle & ~NativeMethods.WS_EX_TRANSPARENT);

        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newExStyle));
    }
}
