using System.Diagnostics;
using Microsoft.Win32;
using KeyDeck.Infrastructure.Config;

namespace KeyDeck.Infrastructure;

/// <summary>
/// Configures whether KeyDeck launches automatically with Windows by writing to
/// or deleting from the <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>
/// registry key.
/// </summary>
/// <remarks>
/// Only the <c>HKCU</c> hive is used — no administrator elevation is required.
/// </remarks>
public static class StartupService
{
    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> if a startup registry entry for KeyDeck exists
    /// in the current user hive.
    /// </summary>
    public static bool IsStartOnBootEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppConstants.StartupRegistryKeyPath, writable: false);
            return key?.GetValue(AppConstants.StartupRegistryValueName) != null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[StartupService] IsStartOnBootEnabled check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enables or disables the Windows startup entry.
    /// When <paramref name="enable"/> is <see langword="true"/>, the current executable
    /// path is written to the registry with the <c>--minimized</c> flag so the app
    /// starts silently in the system tray.
    /// </summary>
    /// <param name="enable">
    /// <see langword="true"/> to register the startup entry;
    /// <see langword="false"/> to remove it.
    /// </param>
    public static void SetStartOnBoot(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppConstants.StartupRegistryKeyPath, writable: true);
            if (key == null)
            {
                Trace.WriteLine("[StartupService] Registry key is null — cannot set startup entry.");
                return;
            }

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    Trace.WriteLine("[StartupService] Cannot determine executable path; startup entry not written.");
                    return;
                }

                key.SetValue(
                    AppConstants.StartupRegistryValueName,
                    $"\"{exePath}\" {AppConstants.StartMinimizedArg}");

                Trace.WriteLine($"[StartupService] Startup entry added: {exePath}");
            }
            else
            {
                key.DeleteValue(AppConstants.StartupRegistryValueName, throwOnMissingValue: false);
                Trace.WriteLine("[StartupService] Startup entry removed.");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[StartupService] SetStartOnBoot({enable}) failed: {ex.Message}");
        }
    }
}
