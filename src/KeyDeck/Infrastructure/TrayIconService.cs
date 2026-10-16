using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure.Config;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Infrastructure;

/// <summary>
/// Provides Windows system tray integration via <see cref="NotifyIcon"/> with an in-memory generated icon,
/// dynamic profile-switching context menu, macro enable/disable toggle, and HUD visibility selection.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IProfileManager _profileManager;
    private readonly AppSettings _settings;
    private readonly KeyInterceptor _keyInterceptor;
    private readonly Action _openSettingsAction;
    private readonly Action _exitAppAction;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class, creating
    /// the system tray icon and binding change events to menu rebuilds.
    /// </summary>
    /// <param name="profileManager">The profile manager instance providing active profiles and profile switching.</param>
    /// <param name="settings">The current application settings.</param>
    /// <param name="keyInterceptor">The keyboard interceptor controlling macro state.</param>
    /// <param name="openSettingsAction">Callback to display the main settings window.</param>
    /// <param name="exitAppAction">Callback to initiate graceful application exit.</param>
    public TrayIconService(IProfileManager profileManager,
                           AppSettings settings,
                           KeyInterceptor keyInterceptor,
                           Action openSettingsAction,
                           Action exitAppAction)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _keyInterceptor = keyInterceptor ?? throw new ArgumentNullException(nameof(keyInterceptor));
        _openSettingsAction = openSettingsAction ?? throw new ArgumentNullException(nameof(openSettingsAction));
        _exitAppAction = exitAppAction ?? throw new ArgumentNullException(nameof(exitAppAction));

        _notifyIcon = new NotifyIcon
        {
            Text = AppConstants.TrayTooltipText,
            Visible = true,
            Icon = GenerateTrayIcon()
        };

        _notifyIcon.DoubleClick += (s, e) => _openSettingsAction();

        RebuildContextMenu();

        _profileManager.ActiveProfileChanged += _ => RebuildContextMenu();
        _profileManager.ProfilesListChanged += RebuildContextMenu;
        _keyInterceptor.MacroModeStateChanged += _ => RebuildContextMenu();
    }

    /// <summary>
    /// Reconstructs the context menu dynamically reflecting current profiles, active profile,
    /// macro toggle state, and HUD overlay visibility mode.
    /// </summary>
    public void RebuildContextMenu()
    {
        try
        {
            var menu = new ContextMenuStrip();

            // Open Settings
            var openItem = new ToolStripMenuItem($"Open {AppConstants.AppName} Settings", null, (s, e) => _openSettingsAction())
            {
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            menu.Items.Add(openItem);

            // Macro Mode Enabled Toggle
            var macroToggleItem = new ToolStripMenuItem("Macro Interception Enabled", null, (s, e) =>
            {
                _settings.IsMacroEnabled = !_settings.IsMacroEnabled;
                _keyInterceptor.NotifyMacroState();
                RebuildContextMenu();
            })
            {
                Checked = _settings.IsMacroEnabled
            };
            menu.Items.Add(macroToggleItem);

            menu.Items.Add(new ToolStripSeparator());

            // Profiles Submenu
            var profilesSubMenu = new ToolStripMenuItem("Profiles");
            foreach (var p in _profileManager.Profiles)
            {
                var profileId = p.Id;
                var item = new ToolStripMenuItem(p.Name, null, (s, e) =>
                {
                    _profileManager.SetActiveProfile(profileId);
                })
                {
                    Checked = _profileManager.ActiveProfile.Id == profileId
                };
                profilesSubMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(profilesSubMenu);

            // HUD Overlay Mode Submenu
            var hudSubMenu = new ToolStripMenuItem("HUD Overlay Mode");
            foreach (HudVisibilityMode mode in Enum.GetValues(typeof(HudVisibilityMode)))
            {
                var currentMode = mode;
                var item = new ToolStripMenuItem(FormatHudMode(currentMode), null, (s, e) =>
                {
                    _settings.HudMode = currentMode;
                    RebuildContextMenu();
                })
                {
                    Checked = _settings.HudMode == currentMode
                };
                hudSubMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(hudSubMenu);

            menu.Items.Add(new ToolStripSeparator());

            // Exit
            menu.Items.Add(new ToolStripMenuItem($"Exit {AppConstants.AppName}", null, (s, e) => _exitAppAction()));

            var oldMenu = _notifyIcon.ContextMenuStrip;
            _notifyIcon.ContextMenuStrip = menu;
            oldMenu?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TrayIconService] Failed to rebuild context menu: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a high-resolution 32x32 in-memory application icon for the system tray.
    /// </summary>
    private static Icon GenerateTrayIcon()
    {
        try
        {
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                // Rounded background rectangle
                using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(0x00, 0x78, 0xD4)))
                {
                    g.FillEllipse(brush, 1, 1, 30, 30);
                }

                // Draw miniature 3x3 keypad grid
                using (var dotBrush = new SolidBrush(System.Drawing.Color.White))
                {
                    int[] coords = { 9, 15, 21 };
                    foreach (var y in coords)
                    {
                        foreach (var x in coords)
                        {
                            g.FillRectangle(dotBrush, x, y, 3, 3);
                        }
                    }
                }
            }

            var hIcon = bitmap.GetHicon();
            var icon = Icon.FromHandle(hIcon);
            var cloned = (Icon)icon.Clone();
            NativeMethods.DestroyIcon(hIcon);
            return cloned;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TrayIconService] Failed to generate tray icon: {ex.Message}");
            return SystemIcons.Application;
        }
    }

    private static string FormatHudMode(HudVisibilityMode mode)
    {
        return mode switch
        {
            HudVisibilityMode.AlwaysOnWhenNumLockOff => "Always On (When Macro Active)",
            HudVisibilityMode.FadeOutOnPress => "Fade Out After Key Press",
            HudVisibilityMode.Disabled => "Disabled",
            _ => mode.ToString()
        };
    }

    /// <summary>
    /// Disposes the tray icon and releases associated Win32 and GDI resources.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.ContextMenuStrip?.Dispose();
                _notifyIcon.Icon?.Dispose();
                _notifyIcon.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[TrayIconService] Error during disposal: {ex.Message}");
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
}
