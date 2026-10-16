using System.Diagnostics;
using System.Threading;
using System.Windows;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.Infrastructure.Config;
using KeyDeck.UI.HudOverlay;

namespace KeyDeck;

/// <summary>
/// Application entry point and root composition root.
/// Responsible for single-instance enforcement, service construction, lifetime management,
/// and providing the application exit path.
/// </summary>
public partial class App : Application
{
    // ── Single-instance enforcement ────────────────────────────────────────
    private static Mutex? _singleInstanceMutex;

    // ── Service graph (owned by App, disposed on exit) ─────────────────────
    private IConfigService?  _configService;
    private AppSettings?     _settings;
    private IProfileManager? _profileManager;
    private NumLockMonitor?  _numLockMonitor;
    private KeyInterceptor?  _keyInterceptor;
    private IActionExecutor? _actionExecutor;
    private HudWindow?       _hudWindow;
    private MainWindow?      _mainWindow;
    private TrayIconService? _trayIconService;

    /// <summary>
    /// <see langword="true"/> from the moment <see cref="ExitApplication"/> is called,
    /// so windows can distinguish a user close (hide to tray) from a real shutdown.
    /// </summary>
    public static bool IsExiting { get; private set; }

    // ── Startup ────────────────────────────────────────────────────────────

    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        // Enforce single-instance
        _singleInstanceMutex = new Mutex(initiallyOwned: true, AppConstants.SingleInstanceMutexName, out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            MessageBox.Show(
                $"{AppConstants.AppName} is already running in the background. Check your system tray.",
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Global unhandled exception guard — log and continue where possible.
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            Trace.WriteLine($"[App] Unhandled exception: {args.ExceptionObject}");

        DispatcherUnhandledException += (s, args) =>
        {
            Trace.WriteLine($"[App] Dispatcher unhandled exception: {args.Exception}");
            // Don't mark as handled — let default WPF behaviour show the dialog.
        };

        try
        {
            // ── Composition root ───────────────────────────────────────────
            _configService  = new ConfigService();
            _settings       = _configService.LoadSettings();
            _profileManager = new ProfileManager(_configService, _settings);
            _numLockMonitor = new NumLockMonitor();
            _keyInterceptor = new KeyInterceptor(_settings, _numLockMonitor);
            _actionExecutor = new ActionExecutor();

            ApplyTheme(_settings.Theme);

            _hudWindow = new HudWindow();
            _hudWindow.SetVisibilityMode(_settings.HudMode);

            _mainWindow = new MainWindow(
                _configService,
                _settings,
                _profileManager,
                _keyInterceptor,
                _actionExecutor,
                _hudWindow);

            _trayIconService = new TrayIconService(
                _profileManager,
                _settings,
                _keyInterceptor,
                openSettingsAction: ShowMainWindow,
                exitAppAction:      ExitApplication);

            // Start hardware keyboard interception last (nothing to intercept before this)
            _keyInterceptor.Start();

            var startMinimized = _settings.StartMinimized ||
                                 e.Args.Contains(AppConstants.StartMinimizedArg);
            if (!startMinimized)
            {
                ShowMainWindow();
            }

            Trace.WriteLine("[App] Startup complete.");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[App] Fatal startup error: {ex}");
            MessageBox.Show(
                $"Failed to initialize {AppConstants.AppName}:\n\n{ex.Message}",
                $"{AppConstants.AppName} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    // ── Public actions ─────────────────────────────────────────────────────

    /// <summary>
    /// Shows and activates the main settings window.
    /// Safe to call from any thread (dispatches to UI thread if needed).
    /// </summary>
    public void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        Dispatcher.InvokeAsync(() =>
        {
            _mainWindow.Show();
            if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _mainWindow.WindowState = System.Windows.WindowState.Normal;
            }
            _mainWindow.Activate();
        });
    }

    /// <summary>
    /// Applies the selected visual theme by swapping the application-level
    /// merged resource dictionary.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    public void ApplyTheme(AppTheme theme)
    {
        var themeUri = theme == AppTheme.Light
            ? new Uri("Resources/Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Resources/Themes/DarkTheme.xaml",  UriKind.Relative);

        var newDict = new System.Windows.ResourceDictionary { Source = themeUri };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(newDict);
    }

    /// <summary>
    /// Persists settings, disposes all services, and shuts down the WPF application.
    /// </summary>
    public void ExitApplication()
    {
        IsExiting = true;

        try
        {
            if (_settings != null && _configService != null)
            {
                _configService.SaveSettings(_settings);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[App] Failed to save settings on exit: {ex.Message}");
        }

        _keyInterceptor?.Dispose();
        _numLockMonitor?.Dispose();
        _trayIconService?.Dispose();
        _hudWindow?.Close();
        _mainWindow?.Close();

        Shutdown();
        Trace.WriteLine("[App] Shutdown complete.");
    }

    // ── Shutdown ───────────────────────────────────────────────────────────

    private void OnAppExit(object sender, ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }
}
