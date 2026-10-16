using System.Windows;
using System.Windows.Interop;

namespace KeyDeck.Platform.Win32;

/// <summary>
/// Configures Win32 window styles for click-through transparent overlays.
/// </summary>
public static class WindowHelper
{
    public static void MakeClickThrough(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var currentExStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

        // WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
        var newExStyle = currentExStyle |
                         NativeMethods.WS_EX_TRANSPARENT |
                         NativeMethods.WS_EX_LAYERED |
                         NativeMethods.WS_EX_TOOLWINDOW |
                         NativeMethods.WS_EX_NOACTIVATE;

        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newExStyle));
    }
}
