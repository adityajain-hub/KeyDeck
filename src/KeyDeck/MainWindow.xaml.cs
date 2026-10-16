using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.UI.Controls;
using KeyDeck.UI.HudOverlay;

namespace KeyDeck;

/// <summary>
/// Code-behind for the three-panel settings window (Profile List | Numpad Grid | Action Editor).
/// Coordinates user interactions between child controls and the service layer via
/// <see cref="IConfigService"/>, <see cref="IProfileManager"/>, and <see cref="IActionExecutor"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class is <em>intentionally thin</em> — it contains only UI-coordination logic.
/// Business rules live in <see cref="ProfileManager"/> and <see cref="ActionExecutor"/>.
/// </para>
/// <para>
/// Closing this window hides it to the system tray rather than exiting the application,
/// unless <see cref="App.IsExiting"/> is <see langword="true"/>.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly IConfigService  _configService;
    private readonly AppSettings     _settings;
    private readonly IProfileManager _profileManager;
    private readonly KeyInterceptor  _keyInterceptor;
    private readonly IActionExecutor _actionExecutor;
    private readonly HudWindow       _hudWindow;

    /// <summary>
    /// Suppresses settings-control event handlers during initial population
    /// to avoid spurious saves on startup.
    /// </summary>
    private bool _isInitializing = true;

    // ── Constructor ────────────────────────────────────────────────────────

    /// <summary>
    /// Constructs the window and wires all child controls to the service graph.
    /// </summary>
    public MainWindow(
        IConfigService  configService,
        AppSettings     settings,
        IProfileManager profileManager,
        KeyInterceptor  keyInterceptor,
        IActionExecutor actionExecutor,
        HudWindow       hudWindow)
    {
        InitializeComponent();

        _configService  = configService  ?? throw new ArgumentNullException(nameof(configService));
        _settings       = settings       ?? throw new ArgumentNullException(nameof(settings));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _keyInterceptor = keyInterceptor ?? throw new ArgumentNullException(nameof(keyInterceptor));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _hudWindow      = hudWindow      ?? throw new ArgumentNullException(nameof(hudWindow));

        // Give the editor a reference to the interceptor (for the bypass-while-recording feature)
        EditorPanel.KeyInterceptor = _keyInterceptor;
        EditorPanel.ActionExecutor = _actionExecutor;

        InitializeSettingsControls();
        WireEvents();
        LoadInitialState();

        _isInitializing = false;
    }

    // ── Initialization ─────────────────────────────────────────────────────

    /// <summary>
    /// Populates settings controls from the current <see cref="AppSettings"/> values.
    /// Must be called with <see cref="_isInitializing"/> set to <see langword="true"/>
    /// to avoid triggering persistence on initial load.
    /// </summary>
    private void InitializeSettingsControls()
    {
        NumLockModeComboBox.ItemsSource  = Enum.GetValues<NumLockMacroMode>();
        NumLockModeComboBox.SelectedItem = _settings.NumLockMode;

        HudModeComboBox.ItemsSource  = Enum.GetValues<HudVisibilityMode>();
        HudModeComboBox.SelectedItem = _settings.HudMode;

        ThemeComboBox.ItemsSource  = Enum.GetValues<AppTheme>();
        ThemeComboBox.SelectedItem = _settings.Theme;

        StartOnBootCheckBox.IsChecked    = _settings.StartOnBoot;
        StartMinimizedCheckBox.IsChecked = _settings.StartMinimized;
        MacroEnabledCheckBox.IsChecked   = _settings.IsMacroEnabled;
    }

    /// <summary>
    /// Subscribes to events from child controls and the service layer.
    /// </summary>
    private void WireEvents()
    {
        // Numpad grid → editor / profile updates
        KeyGrid.KeyCellSelected    += OnKeyCellSelected;
        KeyGrid.KeyCellActivated   += OnNumpadKeyPressed;
        KeyGrid.FileDroppedOnKey   += OnFileDroppedOnKey;
        KeyGrid.NumLockCellClicked += OnNumLockCellClicked;

        // Editor saves
        EditorPanel.ActionModified += OnActionModified;

        // Profile panel operations
        ProfilePanel.ProfileSelected          += OnProfileSelected;
        ProfilePanel.NewProfileRequested      += OnNewProfileRequested;
        ProfilePanel.DuplicateProfileRequested += OnDuplicateProfileRequested;
        ProfilePanel.RenameProfileRequested   += OnRenameProfileRequested;
        ProfilePanel.DeleteProfileRequested   += OnDeleteProfileRequested;

        // Interceptor events → visual feedback
        _keyInterceptor.NumpadKeyPressed     += OnNumpadKeyPressed;
        _keyInterceptor.MacroModeStateChanged += OnMacroModeStateChanged;
    }

    /// <summary>
    /// Pushes the initial data model into all child controls.
    /// </summary>
    private void LoadInitialState()
    {
        ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile);
        KeyGrid.UpdateBindings(_profileManager.ActiveProfile, _configService.IconsDirectory);
        KeyGrid.UpdateNumLockStatus(_keyInterceptor.IsMacroModeActive);

        // Pre-load the editor with the currently selected key
        var initialAction = _profileManager.GetActiveAction(KeyGrid.SelectedKeyId);
        EditorPanel.LoadAction(KeyGrid.SelectedKeyId, initialAction, _configService.IconsDirectory);

        _hudWindow.UpdateProfile(_profileManager.ActiveProfile);
        _hudWindow.UpdateMacroState(_keyInterceptor.IsMacroModeActive);
    }

    // ── Numpad grid handlers ───────────────────────────────────────────────

    private void OnKeyCellSelected(NumpadKeyId keyId)
    {
        var action = _profileManager.GetActiveAction(keyId);
        EditorPanel.LoadAction(keyId, action, _configService.IconsDirectory);
    }

    private void OnFileDroppedOnKey(NumpadKeyId keyId, string filePath)
    {
        // Determine action type from file attributes
        ActionType type;
        try
        {
            type = File.GetAttributes(filePath).HasFlag(FileAttributes.Directory)
                ? ActionType.OpenFolder
                : ActionType.LaunchApp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[MainWindow] OnFileDroppedOnKey: cannot read attributes for '{filePath}': {ex.Message}");
            return;
        }

        var action = new KeyAction
        {
            Type       = type,
            Label      = Path.GetFileNameWithoutExtension(filePath),
            TargetPath = filePath
        };

        // Pre-cache the icon so the grid can display it immediately
        if (action.Type == ActionType.LaunchApp)
        {
            IconExtractor.ExtractOrGetCachedIcon(filePath, _configService.IconsDirectory);
        }

        _profileManager.UpdateBinding(keyId, action);
        RefreshGridAndHud();
        KeyGrid.SelectKey(keyId);
        EditorPanel.LoadAction(keyId, action, _configService.IconsDirectory);
    }

    private void OnNumLockCellClicked()
    {
        // Toggle the master macro switch
        _settings.IsMacroEnabled       = !_settings.IsMacroEnabled;
        MacroEnabledCheckBox.IsChecked  = _settings.IsMacroEnabled;
        _keyInterceptor.NotifyMacroState();
        _configService.SaveSettings(_settings);
    }

    // ── Editor handlers ────────────────────────────────────────────────────

    private void OnActionModified(NumpadKeyId keyId, KeyAction action)
    {
        _profileManager.UpdateBinding(keyId, action);
        RefreshGridAndHud();
    }

    // ── Profile panel handlers ─────────────────────────────────────────────

    private void OnProfileSelected(Profile profile)
    {
        _profileManager.SetActiveProfile(profile.Id);
        KeyGrid.UpdateBindings(profile, _configService.IconsDirectory);

        var action = _profileManager.GetActiveAction(KeyGrid.SelectedKeyId);
        EditorPanel.LoadAction(KeyGrid.SelectedKeyId, action, _configService.IconsDirectory);

        _hudWindow.UpdateProfile(profile);
    }

    private void OnNewProfileRequested()
    {
        var profile = _profileManager.CreateProfile($"Profile {_profileManager.Profiles.Count + 1}");
        ProfilePanel.SetProfiles(_profileManager.Profiles, profile);
        OnProfileSelected(profile);
    }

    private void OnDuplicateProfileRequested(Profile source)
    {
        var duplicate = _profileManager.DuplicateProfile(source.Id, $"{source.Name} (Copy)");
        ProfilePanel.SetProfiles(_profileManager.Profiles, duplicate);
        OnProfileSelected(duplicate);
    }

    private void OnRenameProfileRequested(Profile profile)
    {
        var newName = PromptDialog.Show("Rename Profile", "Enter new profile name:", profile.Name);
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _profileManager.RenameProfile(profile.Id, newName);
            ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile);
            _hudWindow.UpdateProfile(_profileManager.ActiveProfile);
        }
    }

    private void OnDeleteProfileRequested(Profile profile)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete profile '{profile.Name}'?",
            "Delete Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _profileManager.DeleteProfile(profile.Id);
            ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile);
            OnProfileSelected(_profileManager.ActiveProfile);
        }
    }

    // ── KeyInterceptor handlers ────────────────────────────────────────────

    private void OnNumpadKeyPressed(NumpadKeyId keyId)
    {
        // Visual flash on UI thread; action execution is fire-and-forget
        Dispatcher.InvokeAsync(() =>
        {
            KeyGrid.FlashKey(keyId);
            _hudWindow.FlashKey(keyId);
        });

        var action = _profileManager.GetActiveAction(keyId);
        _ = _actionExecutor.ExecuteAsync(action);
    }

    private void OnMacroModeStateChanged(bool isMacroActive)
    {
        Dispatcher.InvokeAsync(() =>
        {
            KeyGrid.UpdateNumLockStatus(isMacroActive);
            _hudWindow.UpdateMacroState(isMacroActive);
        });
    }

    // ── Settings panel handlers ────────────────────────────────────────────

    private void OnMacroEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.IsMacroEnabled = MacroEnabledCheckBox.IsChecked == true;
        _keyInterceptor.NotifyMacroState();
        _configService.SaveSettings(_settings);
    }

    private void OnStartOnBootClicked(object sender, RoutedEventArgs e)
    {
        _settings.StartOnBoot = StartOnBootCheckBox.IsChecked == true;
        StartupService.SetStartOnBoot(_settings.StartOnBoot);
        _configService.SaveSettings(_settings);
    }

    private void OnStartMinimizedClicked(object sender, RoutedEventArgs e)
    {
        _settings.StartMinimized = StartMinimizedCheckBox.IsChecked == true;
        _configService.SaveSettings(_settings);
    }

    private void OnNumLockModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (NumLockModeComboBox.SelectedItem is NumLockMacroMode mode)
        {
            _settings.NumLockMode = mode;
            _keyInterceptor.NotifyMacroState();
            _configService.SaveSettings(_settings);
        }
    }

    private void OnHudModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (HudModeComboBox.SelectedItem is HudVisibilityMode mode)
        {
            _settings.HudMode = mode;
            _hudWindow.SetVisibilityMode(mode);
            _configService.SaveSettings(_settings);
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (ThemeComboBox.SelectedItem is AppTheme theme)
        {
            _settings.Theme = theme;
            ((App)Application.Current).ApplyTheme(theme);
            _configService.SaveSettings(_settings);
        }
    }

    // ── Window lifecycle ───────────────────────────────────────────────────

    /// <summary>
    /// Intercepts the window close button: hides to tray unless the app is actually exiting.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the numpad grid and HUD overlay after a binding or profile change.
    /// </summary>
    private void RefreshGridAndHud()
    {
        KeyGrid.UpdateBindings(_profileManager.ActiveProfile, _configService.IconsDirectory);
        _hudWindow.UpdateProfile(_profileManager.ActiveProfile);
    }
}
