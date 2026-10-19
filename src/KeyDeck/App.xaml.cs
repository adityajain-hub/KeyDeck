using System.Diagnostics;
using System.Threading;
using System.Windows;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.Infrastructure.Config;
using KeyDeck.Platform.Win32;
using KeyDeck.UI.HudOverlay;

namespace KeyDeck;

/// <summary>
/// Application entry point and root composition root.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _showWindowEvent;
    private static RegisteredWaitHandle? _registeredWaitHandle;

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
        bool isOnlyInstance = false;
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
                _hasMutex = _singleInstanceMutex.WaitOne(TimeSpan.FromMilliseconds(500), true);
            }
            catch (AbandonedMutexException)
            {
                _hasMutex = true;
            }
        }

        if (!_hasMutex)
        {
            // Another instance is already running in the background.
            // Signal the running instance to restore and bring its main window to the foreground.
            try
            {
                if (EventWaitHandle.TryOpenExisting(AppConstants.SingleInstanceEventName, out var signalHandle))
                {
                    signalHandle.Set();
                    signalHandle.Dispose();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[App] Failed to signal running instance: {ex.Message}");
            }

            Environment.Exit(0);
            return;
        }

        try
        {
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.SingleInstanceEventName);
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _showWindowEvent,
                (state, timedOut) =>
                {
                    if (!IsExiting)
                    {
                        ShowMainWindow();
                    }
                },
                null,
                -1,
                false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[App] Failed to register single-instance show window event: {ex.Message}");
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
            try
            {
                var logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyDeck");
                System.IO.Directory.CreateDirectory(logDir);
                var logPath = System.IO.Path.Combine(logDir, "startup.log");
                System.IO.File.AppendAllText(logPath, $"[Fatal startup error] {ex}\n");
            }
            catch { }
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
            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.Focus();

            var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
            }

            // Topmost bump forces Windows to bring the window in front of any active application:
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
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

        _registeredWaitHandle?.Unregister(null);
        _showWindowEvent?.Dispose();
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
        _registeredWaitHandle?.Unregister(null);
        _showWindowEvent?.Dispose();

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
