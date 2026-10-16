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
/// </summary>
public partial class MainWindow : Window
{
    private readonly IConfigService   _configService;
    private readonly AppSettings      _settings;
    private readonly IProfileManager  _profileManager;
    private readonly IKeyInterceptor  _keyInterceptor;
    private readonly IActionExecutor  _actionExecutor;
    private readonly HudWindow        _hudWindow;
    private readonly IAppStateService _appStateService;

    public TrayIconService? TrayIconService { get; set; }
    public IForegroundAppMonitor? ForegroundAppMonitor { get; set; }

    private bool _isInitializing = true;

    public MainWindow(
        IConfigService   configService,
        AppSettings      settings,
        IProfileManager  profileManager,
        IKeyInterceptor  keyInterceptor,
        IActionExecutor  actionExecutor,
        HudWindow        hudWindow,
        IAppStateService? appStateService = null)
    {
        InitializeComponent();

        _configService   = configService  ?? throw new ArgumentNullException(nameof(configService));
        _settings        = settings       ?? throw new ArgumentNullException(nameof(settings));
        _profileManager  = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _keyInterceptor  = keyInterceptor ?? throw new ArgumentNullException(nameof(keyInterceptor));
        _actionExecutor  = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _hudWindow       = hudWindow      ?? throw new ArgumentNullException(nameof(hudWindow));
        _appStateService = appStateService ?? new KeyDeck.Core.State.AppStateService(_settings, _configService, _profileManager);

        EditorPanel.KeyInterceptor = _keyInterceptor;
        EditorPanel.ActionExecutor = _actionExecutor;
        EditorPanel.ProfileManager = _profileManager;

        InitializeSettingsControls();
        WireEvents();
        LoadInitialState();

        _isInitializing = false;
    }

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
        MacroEnabledCheckBox.IsChecked     = _settings.IsMacroEnabled;
        AutoSwitchHeaderCheckBox.IsChecked = _settings.IsAutoProfileSwitchEnabled;
        LockHudCheckBox.IsChecked          = _settings.IsHudLocked;

        var clampedOpacity = Math.Clamp(_settings.HudOpacity, 0.20, 1.0);
        HudOpacitySlider.Value  = clampedOpacity;
        HudOpacityValueText.Text = $"{(int)Math.Round(clampedOpacity * 100)}%";
    }

    /// <summary>
    /// Synchronizes MainWindow controls and HUD state when settings are changed from the system tray.
    /// </summary>
    public void SyncSettingsUiFromExternalChange()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _isInitializing = true;

            MacroEnabledCheckBox.IsChecked     = _settings.IsMacroEnabled;
            AutoSwitchHeaderCheckBox.IsChecked = _settings.IsAutoProfileSwitchEnabled;
            ProfilePanel.SetAutoSwitchChecked(_settings.IsAutoProfileSwitchEnabled);
            HudModeComboBox.SelectedItem       = _settings.HudMode;
            LockHudCheckBox.IsChecked          = _settings.IsHudLocked;

            var clampedOpacity = Math.Clamp(_settings.HudOpacity, 0.20, 1.0);
            HudOpacitySlider.Value   = clampedOpacity;
            HudOpacityValueText.Text = $"{(int)Math.Round(clampedOpacity * 100)}%";

            ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile, _settings.IsAutoProfileSwitchEnabled);
            RefreshGridAndHud();

            _hudWindow.SetVisibilityMode(_settings.HudMode);
            _hudWindow.SetLockedState(_settings.IsHudLocked);
            _hudWindow.SetHudOpacity(_settings.HudOpacity);

            _isInitializing = false;
        });
    }

    public void ResetHudPosition()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _hudWindow.ResetPositionToBottomRight();
        });
    }

    private void WireEvents()
    {
        KeyGrid.KeyCellSelected    += OnKeyCellSelected;
        KeyGrid.KeyCellActivated   += OnNumpadKeyPressed;
        KeyGrid.FileDroppedOnKey   += OnFileDroppedOnKey;
        KeyGrid.NumLockCellClicked += OnNumLockCellClicked;

        EditorPanel.ActionModified += OnActionModified;

        ProfilePanel.ProfileSelected           += OnProfileSelected;
        ProfilePanel.NewProfileRequested       += OnNewProfileRequested;
        ProfilePanel.DuplicateProfileRequested += OnDuplicateProfileRequested;
        ProfilePanel.RenameProfileRequested    += OnRenameProfileRequested;
        ProfilePanel.DeleteProfileRequested    += OnDeleteProfileRequested;
        ProfilePanel.AutoSwitchToggled         += OnProfilePanelAutoSwitchToggled;
        ProfilePanel.ProfileLinkedAppChanged   += OnProfileLinkedAppChanged;

        _profileManager.ActiveProfileChanged += OnActiveProfileChangedFromManager;
        _profileManager.ProfilesListChanged  += () => Dispatcher.InvokeAsync(() => EditorPanel.RefreshProfilesDropdown());

        _keyInterceptor.NumpadKeyPressed      += OnNumpadKeyPressed;
        _keyInterceptor.MacroModeStateChanged += OnMacroModeStateChanged;

        _hudWindow.PositionChanged += OnHudPositionChanged;
        _hudWindow.HideRequested   += OnHudHideRequested;

        _appStateService.StateChanged += OnAppStateChanged;
        _appStateService.ResetHudPositionRequested += ResetHudPosition;
    }

    private void LoadInitialState()
    {
        ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile, _settings.IsAutoProfileSwitchEnabled);
        KeyGrid.UpdateBindings(_profileManager.ActiveProfile, _configService.IconsDirectory);
        KeyGrid.UpdateNumLockStatus(_keyInterceptor.IsMacroModeActive);

        var initialAction = _profileManager.GetActiveAction(KeyGrid.SelectedKeyId);
        EditorPanel.LoadAction(KeyGrid.SelectedKeyId, initialAction, _configService.IconsDirectory);

        _hudWindow.ConfigureFromSettings(_settings, _configService.IconsDirectory);
        _hudWindow.UpdateProfile(_profileManager.ActiveProfile, _configService.IconsDirectory);
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
        ActionType type;
        try
        {
            if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
            {
                type = ActionType.OpenFolder;
            }
            else
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                type = ext is ".ps1" or ".bat" or ".cmd"
                    ? ActionType.RunScript
                    : ActionType.LaunchApp;
            }
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
        _settings.IsMacroEnabled       = !_settings.IsMacroEnabled;
        MacroEnabledCheckBox.IsChecked = _settings.IsMacroEnabled;
        _keyInterceptor.NotifyMacroState();
        _configService.SaveSettings(_settings);
        TrayIconService?.RebuildContextMenu();
    }

    // ── Editor handlers ────────────────────────────────────────────────────

    private void OnActionModified(NumpadKeyId keyId, KeyAction action)
    {
        _profileManager.UpdateBinding(keyId, action);
        RefreshGridAndHud();
    }

    // ── Profile panel handlers ─────────────────────────────────────────────

    private void OnActiveProfileChangedFromManager(Profile profile)
    {
        Dispatcher.InvokeAsync(() =>
        {
            ProfilePanel.SetProfiles(_profileManager.Profiles, profile);
            KeyGrid.UpdateBindings(profile, _configService.IconsDirectory);

            var action = _profileManager.GetActiveAction(KeyGrid.SelectedKeyId);
            EditorPanel.LoadAction(KeyGrid.SelectedKeyId, action, _configService.IconsDirectory);

            _hudWindow.UpdateProfile(profile, _configService.IconsDirectory);
        });
    }

    private void OnProfileSelected(Profile profile)
    {
        _profileManager.SetActiveProfile(profile.Id);
        KeyGrid.UpdateBindings(profile, _configService.IconsDirectory);

        var action = _profileManager.GetActiveAction(KeyGrid.SelectedKeyId);
        EditorPanel.LoadAction(KeyGrid.SelectedKeyId, action, _configService.IconsDirectory);

        _hudWindow.UpdateProfile(profile, _configService.IconsDirectory);
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
            ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile, _settings.IsAutoProfileSwitchEnabled);
            _hudWindow.UpdateProfile(_profileManager.ActiveProfile, _configService.IconsDirectory);
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
            ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile, _settings.IsAutoProfileSwitchEnabled);
            OnProfileSelected(_profileManager.ActiveProfile);
        }
    }

    // ── KeyInterceptor handlers ────────────────────────────────────────────

    private void OnNumpadKeyPressed(NumpadKeyId keyId)
    {
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

    // ── Reactive State Synchronization ─────────────────────────────────────

    private void OnAppStateChanged(AppStateChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _isInitializing = true;
            try
            {
                switch (e.Property)
                {
                    case AppStateProperty.MacroInterception:
                        MacroEnabledCheckBox.IsChecked = _appStateService.IsMacroEnabled;
                        _keyInterceptor.NotifyMacroState();
                        break;

                    case AppStateProperty.AutoProfileSwitch:
                        AutoSwitchHeaderCheckBox.IsChecked = _appStateService.IsAutoProfileSwitchEnabled;
                        ProfilePanel.SetAutoSwitchChecked(_appStateService.IsAutoProfileSwitchEnabled);
                        if (_appStateService.IsAutoProfileSwitchEnabled)
                        {
                            ForegroundAppMonitor?.EvaluateCurrentForegroundWindow();
                        }
                        break;

                    case AppStateProperty.HudVisibilityMode:
                        HudModeComboBox.SelectedItem = _appStateService.HudMode;
                        _hudWindow.SetVisibilityMode(_appStateService.HudMode);
                        break;

                    case AppStateProperty.HudOpacity:
                        HudOpacitySlider.Value = _appStateService.HudOpacity;
                        HudOpacityValueText.Text = $"{(int)Math.Round(_appStateService.HudOpacity * 100)}%";
                        _hudWindow.SetHudOpacity(_appStateService.HudOpacity);
                        break;

                    case AppStateProperty.HudLockedState:
                        LockHudCheckBox.IsChecked = _appStateService.IsHudLocked;
                        _hudWindow.SetLockedState(_appStateService.IsHudLocked);
                        break;

                    case AppStateProperty.Theme:
                        ThemeComboBox.SelectedItem = _appStateService.Theme;
                        break;

                    case AppStateProperty.ActiveProfile:
                        if (e.NewValue is Profile profile)
                        {
                            ProfilePanel.SetProfiles(_profileManager.Profiles, profile, _appStateService.IsAutoProfileSwitchEnabled);
                            KeyGrid.UpdateBindings(profile, _configService.IconsDirectory);
                            var action = _profileManager.GetActiveAction(KeyGrid.SelectedKeyId);
                            EditorPanel.LoadAction(KeyGrid.SelectedKeyId, action, _configService.IconsDirectory);
                            _hudWindow.UpdateProfile(profile, _configService.IconsDirectory);
                        }
                        break;
                }
            }
            finally
            {
                _isInitializing = false;
            }
        });
    }

    // ── HUD position / close handlers ──────────────────────────────────────

    private void OnHudPositionChanged(double left, double top)
    {
        _appStateService.SetHudPosition(left, top);
    }

    private void OnHudHideRequested()
    {
        _appStateService.SetHudMode(HudVisibilityMode.Disabled);
    }

    // ── Settings panel handlers ────────────────────────────────────────────

    private void OnAutoSwitchHeaderChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SetAutoProfileSwitchEnabled(AutoSwitchHeaderCheckBox.IsChecked == true);
    }

    private void OnProfilePanelAutoSwitchToggled(bool isEnabled)
    {
        if (_isInitializing) return;
        SetAutoProfileSwitchEnabled(isEnabled);
    }

    private void SetAutoProfileSwitchEnabled(bool isEnabled)
    {
        _appStateService.SetAutoProfileSwitchEnabled(isEnabled);
    }

    private void OnProfileLinkedAppChanged(Profile profile, string? linkedExe)
    {
        _profileManager.SetProfileLinkedApp(profile.Id, linkedExe);
        ProfilePanel.SetProfiles(_profileManager.Profiles, _profileManager.ActiveProfile, _settings.IsAutoProfileSwitchEnabled);

        if (_settings.IsAutoProfileSwitchEnabled)
        {
            ForegroundAppMonitor?.EvaluateCurrentForegroundWindow();
        }
    }

    private void OnMacroEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appStateService.SetMacroEnabled(MacroEnabledCheckBox.IsChecked == true);
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
            _appStateService.SetHudMode(mode);
        }
    }

    private void OnHudOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var rounded = Math.Round(e.NewValue, 2);
        if (HudOpacityValueText != null)
        {
            HudOpacityValueText.Text = $"{(int)Math.Round(rounded * 100)}%";
        }

        if (_isInitializing) return;
        _appStateService.SetHudOpacity(rounded);
    }

    private void OnHudOpacityDownClick(object sender, RoutedEventArgs e)
    {
        HudOpacitySlider.Value = Math.Max(HudOpacitySlider.Minimum, Math.Round(HudOpacitySlider.Value - 0.05, 2));
    }

    private void OnHudOpacityUpClick(object sender, RoutedEventArgs e)
    {
        HudOpacitySlider.Value = Math.Min(HudOpacitySlider.Maximum, Math.Round(HudOpacitySlider.Value + 0.05, 2));
    }

    private void OnLockHudChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appStateService.SetHudLocked(LockHudCheckBox.IsChecked == true);
    }

    private void OnResetHudPositionClick(object sender, RoutedEventArgs e)
    {
        _appStateService.RequestResetHudPosition();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (ThemeComboBox.SelectedItem is AppTheme theme)
        {
            _appStateService.SetTheme(theme);
            ((App)Application.Current).ApplyTheme(theme);
        }
    }

    // ── Window lifecycle ───────────────────────────────────────────────────

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

    private void RefreshGridAndHud()
    {
        KeyGrid.UpdateBindings(_profileManager.ActiveProfile, _configService.IconsDirectory);
        _hudWindow.UpdateProfile(_profileManager.ActiveProfile, _configService.IconsDirectory);
    }
}
