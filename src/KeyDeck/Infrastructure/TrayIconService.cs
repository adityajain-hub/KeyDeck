using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure.Config;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Infrastructure;

/// <summary>
/// Provides Windows system tray integration via <see cref="NotifyIcon"/> with an in-memory generated icon,
/// dynamic profile-switching context menu, macro enable/disable toggle, and HUD visibility/lock controls.
/// Fully synchronized with <see cref="IAppStateService"/>.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IAppStateService _appStateService;
    private readonly IProfileManager _profileManager;
    private readonly IKeyInterceptor _keyInterceptor;
    private readonly Action _openSettingsAction;
    private readonly Action _exitAppAction;
    private bool _isDisposed;

    public TrayIconService(
        IAppStateService appStateService,
        IProfileManager profileManager,
        IKeyInterceptor keyInterceptor,
        Action openSettingsAction,
        Action exitAppAction)
    {
        _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
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

        _appStateService.StateChanged += e =>
        {
            if (e.Property is AppStateProperty.HudOpacity or AppStateProperty.HudPosition or AppStateProperty.Theme)
            {
                return;
            }
            RebuildContextMenu();
        };
        _profileManager.ProfilesListChanged += RebuildContextMenu;
        _keyInterceptor.MacroModeStateChanged += _ => RebuildContextMenu();
    }

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
                _appStateService.SetMacroEnabled(!_appStateService.IsMacroEnabled);
            })
            {
                Checked = _appStateService.IsMacroEnabled
            };
            menu.Items.Add(macroToggleItem);

            // Per-App Auto-Profile Switching Toggle
            var autoSwitchItem = new ToolStripMenuItem("Per-App Auto-Profile Switch", null, (s, e) =>
            {
                _appStateService.SetAutoProfileSwitchEnabled(!_appStateService.IsAutoProfileSwitchEnabled);
            })
            {
                Checked = _appStateService.IsAutoProfileSwitchEnabled
            };
            menu.Items.Add(autoSwitchItem);

            menu.Items.Add(new ToolStripSeparator());

            // Profiles Submenu
            var profilesSubMenu = new ToolStripMenuItem("Profiles");
            foreach (var p in _profileManager.Profiles)
            {
                var profileId = p.Id;
                var item = new ToolStripMenuItem(p.Name, null, (s, e) =>
                {
                    _appStateService.SetActiveProfile(profileId);
                })
                {
                    Checked = _appStateService.ActiveProfile.Id == profileId
                };
                profilesSubMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(profilesSubMenu);

            // HUD Overlay Submenu
            var hudSubMenu = new ToolStripMenuItem("HUD Overlay");
            foreach (HudVisibilityMode mode in Enum.GetValues(typeof(HudVisibilityMode)))
            {
                var currentMode = mode;
                var item = new ToolStripMenuItem(FormatHudMode(currentMode), null, (s, e) =>
                {
                    _appStateService.SetHudMode(currentMode);
                })
                {
                    Checked = _appStateService.HudMode == currentMode
                };
                hudSubMenu.DropDownItems.Add(item);
            }

            hudSubMenu.DropDownItems.Add(new ToolStripSeparator());

            var lockHudItem = new ToolStripMenuItem("Lock HUD Position (Click-Through)", null, (s, e) =>
            {
                _appStateService.SetHudLocked(!_appStateService.IsHudLocked);
            })
            {
                Checked = _appStateService.IsHudLocked
            };
            hudSubMenu.DropDownItems.Add(lockHudItem);

            var resetHudPosItem = new ToolStripMenuItem("Reset HUD to Bottom-Right", null, (s, e) =>
            {
                _appStateService.RequestResetHudPosition();
            });
            hudSubMenu.DropDownItems.Add(resetHudPosItem);

            menu.Items.Add(hudSubMenu);

            menu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem("Exit KeyDeck", null, (s, e) => _exitAppAction());
            menu.Items.Add(exitItem);

            var oldMenu = _notifyIcon.ContextMenuStrip;
            _notifyIcon.ContextMenuStrip = menu;
            oldMenu?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TrayIconService] RebuildContextMenu failed: {ex.Message}");
        }
    }

    private static Icon GenerateTrayIcon()
    {
        try
        {
            var appIcoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(appIcoPath))
            {
                return new Icon(appIcoPath, 32, 32);
            }

            try
            {
                var resourceStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
                if (resourceStream?.Stream != null)
                {
                    using (resourceStream.Stream)
                    {
                        return new Icon(resourceStream.Stream, 32, 32);
                    }
                }
            }
            catch
            {
                // Fall back to process icon extraction
            }

            if (!string.IsNullOrEmpty(Environment.ProcessPath))
            {
                var exeIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (exeIcon != null) return exeIcon;
            }

            using (var bmp = new Bitmap(32, 32))
            {
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(0x00, 0x78, 0xD4)))
                {
                    g.FillEllipse(brush, 1, 1, 30, 30);
                }

                using (var dotBrush = new SolidBrush(System.Drawing.Color.White))
                {
                    int[] coords = { 9, 15, 21 };
                    foreach (var x in coords)
                    {
                        foreach (var y in coords)
                        {
                            g.FillRectangle(dotBrush, x, y, 2, 2);
                        }
                    }
                }

                var hIcon = bmp.GetHicon();
                try
                {
                    using var tempIcon = Icon.FromHandle(hIcon);
                    return (Icon)tempIcon.Clone();
                }
                finally
                {
                    NativeMethods.DestroyIcon(hIcon);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TrayIconService] GenerateTrayIcon failed, using SystemIcons.Application: {ex.Message}");
            return SystemIcons.Application;
        }
    }

    private static string FormatHudMode(HudVisibilityMode mode)
    {
        return mode switch
        {
            HudVisibilityMode.AlwaysOnWhenNumLockOff => "Always On (When Macro Active)",
            HudVisibilityMode.FadeOutOnPress => "Fade Out After Key Press",
            HudVisibilityMode.Disabled => "Disabled (Remove HUD)",
            _ => mode.ToString()
        };
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _isDisposed = true;
        }
    }
}
