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
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    private IConfigService?        _configService;
    private AppSettings?           _settings;
    private IProfileManager?       _profileManager;
    private KeyDeck.Core.Abstractions.IAppStateService?      _appStateService;
    private KeyDeck.Core.Abstractions.INumLockMonitor?       _numLockMonitor;
    private KeyDeck.Core.Abstractions.IKeyInterceptor?       _keyInterceptor;
    private KeyDeck.Core.Abstractions.IForegroundAppMonitor? _foregroundAppMonitor;
    private IActionExecutor?       _actionExecutor;
    private HudWindow?             _hudWindow;
    private MainWindow?            _mainWindow;
    private TrayIconService?       _trayIconService;

    public static bool IsExiting { get; private set; }

    private bool _hasMutex;

    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        bool isOnlyInstance;
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, AppConstants.SingleInstanceMutexName, out isOnlyInstance);
            _hasMutex = isOnlyInstance;
        }
        catch (AbandonedMutexException)
        {
            _hasMutex = true;
            isOnlyInstance = true;
        }

        if (!_hasMutex && _singleInstanceMutex != null)
        {
            try
            {
                _hasMutex = _singleInstanceMutex.WaitOne(TimeSpan.Zero, true);
            }
            catch (AbandonedMutexException)
            {
                _hasMutex = true;
            }
        }

        if (!_hasMutex)
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

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            Trace.WriteLine($"[App] Unhandled exception: {args.ExceptionObject}");

        DispatcherUnhandledException += (s, args) =>
        {
            Trace.WriteLine($"[App] Dispatcher unhandled exception: {args.Exception}");
        };

        try
        {
            _configService  = new ConfigService();
            _settings       = _configService.LoadSettings();
            _profileManager = new ProfileManager(_configService, _settings);
            _numLockMonitor = new NumLockMonitor();
            _keyInterceptor = new KeyInterceptor(_settings, _numLockMonitor)
            {
                AllowKeyRepeatPredicate = keyId =>
                {
                    var action = _profileManager.GetActiveAction(keyId);
                    return action.Type == ActionType.MediaControl &&
                           (action.MediaCommand == MediaCommand.VolumeUp ||
                            action.MediaCommand == MediaCommand.VolumeDown);
                }
            };
            _actionExecutor = new ActionExecutor(_profileManager);

            _foregroundAppMonitor = new ForegroundAppMonitor(_settings);
            _foregroundAppMonitor.ForegroundProcessChanged += processName =>
            {
                _profileManager.TryActivateProfileForProcess(processName);
            };

            ApplyTheme(_settings.Theme);

            _appStateService = new KeyDeck.Core.State.AppStateService(_settings, _configService, _profileManager);

            _hudWindow = new HudWindow();
            _hudWindow.ConfigureFromSettings(_settings, _configService.IconsDirectory);

            _mainWindow = new MainWindow(
                _configService,
                _settings,
                _profileManager,
                _keyInterceptor,
                _actionExecutor,
                _hudWindow,
                _appStateService);

            _trayIconService = new TrayIconService(
                _appStateService,
                _profileManager,
                _keyInterceptor,
                openSettingsAction: ShowMainWindow,
                exitAppAction:      ExitApplication);

            _mainWindow.TrayIconService      = _trayIconService;
            _mainWindow.ForegroundAppMonitor = _foregroundAppMonitor;

            _keyInterceptor.Start();
            _foregroundAppMonitor.Start();

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

    public void ApplyTheme(AppTheme theme)
    {
        var themeUri = theme == AppTheme.Light
            ? new Uri("Resources/Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Resources/Themes/DarkTheme.xaml",  UriKind.Relative);

        var newDict = new System.Windows.ResourceDictionary { Source = themeUri };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(newDict);
    }

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

        _foregroundAppMonitor?.Dispose();
        _keyInterceptor?.Dispose();
        _numLockMonitor?.Dispose();
        _trayIconService?.Dispose();
        _hudWindow?.Close();
        _mainWindow?.Close();

        Shutdown();
        Trace.WriteLine("[App] Shutdown complete.");
    }

    private void OnAppExit(object sender, ExitEventArgs e)
    {
        if (_hasMutex && _singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[App] Failed to release mutex: {ex.Message}");
            }
        }
        _singleInstanceMutex?.Dispose();
    }
}
