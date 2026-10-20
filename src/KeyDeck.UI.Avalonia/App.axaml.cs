using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Core.State;
using KeyDeck.Infrastructure;
using KeyDeck.Platform.Linux.Interception;
using KeyDeck.Platform.Linux.Services;
using KeyDeck.Platform.Linux.VirtualKeyboard;
using KeyDeck.UI.Avalonia.Views;

namespace KeyDeck.UI.Avalonia;

public partial class App : Application
{
    public static App Instance => (App)Current!;

    public IConfigService ConfigService { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public IProfileManager ProfileManager { get; private set; } = null!;
    public IAppStateService AppState { get; private set; } = null!;
    public IActionExecutor ActionExecutor { get; private set; } = null!;
    public IKeyInterceptor KeyInterceptor { get; private set; } = null!;
    public INumLockMonitor? NumLockMonitor { get; private set; }
    public IForegroundAppMonitor? ForegroundAppMonitor { get; private set; }

    public MainWindow? MainWindowInstance { get; private set; }
    public HudWindow? HudWindowInstance { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Initialize Core services
        ConfigService = new ConfigService();
        Settings = ConfigService.LoadSettings();
        ProfileManager = new ProfileManager(ConfigService, Settings);
        AppState = new AppStateService(Settings, ConfigService, ProfileManager);

        // Linux platform services
        NumLockMonitor = new LinuxNumLockMonitor();
        var virtualKb = new UInputVirtualKeyboard();
        ActionExecutor = new LinuxActionExecutor(virtualKb, ProfileManager);
        KeyInterceptor = new EvdevKeyboardInterceptor(Settings, NumLockMonitor, virtualKb);
        ForegroundAppMonitor = new LinuxForegroundAppMonitor();

        // Wire auto-profile switching
        ForegroundAppMonitor.ForegroundProcessChanged += OnForegroundProcessChanged;
        if (Settings.IsAutoProfileSwitchEnabled)
        {
            ForegroundAppMonitor.Start();
        }

        // Wire key interception to action execution
        KeyInterceptor.NumpadKeyPressed += OnNumpadKeyPressed;
        KeyInterceptor.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow(AppState, ProfileManager, ActionExecutor, KeyInterceptor, NumLockMonitor);
            MainWindowInstance = mainWindow;
            desktop.MainWindow = mainWindow;

            // Initialize HUD Window
            HudWindowInstance = new HudWindow(AppState, ProfileManager);
            if (Settings.HudMode != HudVisibilityMode.Disabled)
            {
                HudWindowInstance.Show();
            }

            // Setup Tray Icon
            SetupTrayIcon(desktop);

            // Handle start minimized setting
            if (Settings.StartMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var showItem = new NativeMenuItem("Open KeyDeck");
            showItem.Click += (_, _) => ShowMainWindow();

            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += (_, _) =>
            {
                KeyInterceptor.Dispose();
                desktop.Shutdown();
            };

            var menu = new NativeMenu();
            menu.Items.Add(showItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exitItem);

            var trayIcon = new TrayIcon
            {
                ToolTipText = "KeyDeck — Numpad Macro Pad",
                Menu = menu,
                IsVisible = true
            };
            trayIcon.Clicked += (_, _) => ShowMainWindow();

            var trayIcons = new TrayIcons { trayIcon };
            TrayIcon.SetIcons(this, trayIcons);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[App] Error initializing tray icon: {ex.Message}");
        }
    }

    public void ShowMainWindow()
    {
        if (MainWindowInstance != null)
        {
            MainWindowInstance.Show();
            MainWindowInstance.WindowState = WindowState.Normal;
            MainWindowInstance.Activate();
        }
    }

    private async void OnNumpadKeyPressed(NumpadKeyId keyId)
    {
        var action = ProfileManager.GetActiveAction(keyId);
        if (action.Type != ActionType.None)
        {
            await ActionExecutor.ExecuteAsync(action).ConfigureAwait(false);
        }
    }

    private void OnForegroundProcessChanged(string processName)
    {
        if (Settings.IsAutoProfileSwitchEnabled)
        {
            ProfileManager.TryActivateProfileForProcess(processName);
        }
    }
}
