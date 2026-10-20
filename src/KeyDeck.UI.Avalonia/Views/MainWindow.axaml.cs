using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly IAppStateService _appState = null!;
    private readonly IProfileManager _profileManager = null!;
    private readonly IActionExecutor _actionExecutor = null!;
    private readonly IKeyInterceptor _keyInterceptor = null!;
    private readonly INumLockMonitor? _numLockMonitor;

    public MainWindow() { InitializeComponent(); }

    public MainWindow(
        IAppStateService appState,
        IProfileManager profileManager,
        IActionExecutor actionExecutor,
        IKeyInterceptor keyInterceptor,
        INumLockMonitor? numLockMonitor)
    {
        InitializeComponent();

        _appState = appState;
        _profileManager = profileManager;
        _actionExecutor = actionExecutor;
        _keyInterceptor = keyInterceptor;
        _numLockMonitor = numLockMonitor;

        if (_appState == null) return;

        // Initialize child panels
        ProfileList.Initialize(_profileManager);
        ActionEditor.Initialize(_profileManager, _actionExecutor);

        // Populate bottom ComboBoxes
        NumLockModeComboBox.ItemsSource = Enum.GetValues<NumLockMacroMode>();
        NumLockModeComboBox.SelectedItem = _appState.Settings.NumLockMode;

        HudModeComboBox.ItemsSource = Enum.GetValues<HudVisibilityMode>();
        HudModeComboBox.SelectedItem = _appState.Settings.HudMode;

        // Sync header controls
        MacroEnabledCheckBox.IsChecked = _appState.IsMacroEnabled;
        AutoSwitchHeaderCheckBox.IsChecked = _appState.IsAutoProfileSwitchEnabled;

        // Sync bottom bar controls
        StartOnBootCheckBox.IsChecked = _appState.Settings.StartOnBoot;
        StartMinimizedCheckBox.IsChecked = _appState.Settings.StartMinimized;
        LockHudCheckBox.IsChecked = _appState.IsHudLocked;
        HudOpacitySlider.Value = _appState.HudOpacity;
        HudOpacityText.Text = $"{(int)(_appState.HudOpacity * 100)}%";

        // Wire coordination between panels
        KeyGrid.KeySelected += OnKeySelected;
        ProfileList.ProfileSelected += OnProfileSelected;
        ActionEditor.BindingModified += OnBindingModified;

        _profileManager.ActiveProfileChanged += OnActiveProfileChanged;
        _keyInterceptor.NumpadKeyPressed += OnNumpadKeyPressed;

        if (_numLockMonitor != null)
        {
            _numLockMonitor.NumLockChanged += OnNumLockChanged;
        }

        // Initial render of active profile
        KeyGrid.RefreshProfile(_profileManager.ActiveProfile);
        KeyGrid.SelectKey(NumpadKeyId.Numpad7);
        ActionEditor.LoadKeyBinding(NumpadKeyId.Numpad7);
        UpdateNumLockUi();
    }

    private void OnKeySelected(NumpadKeyId keyId)
    {
        ActionEditor.LoadKeyBinding(keyId);
    }

    private void OnProfileSelected(Profile profile)
    {
        KeyGrid.RefreshProfile(profile);
        ActionEditor.LoadKeyBinding(KeyGrid.SelectedKey);
    }

    private void OnActiveProfileChanged(Profile profile)
    {
        KeyGrid.RefreshProfile(profile);
        ActionEditor.LoadKeyBinding(KeyGrid.SelectedKey);
    }

    private void OnBindingModified(NumpadKeyId keyId, KeyAction action)
    {
        KeyGrid.RefreshProfile(_profileManager.ActiveProfile);
    }

    private void OnNumpadKeyPressed(NumpadKeyId keyId)
    {
        KeyGrid.FlashKey(keyId);
    }

    private void OnNumLockChanged(bool isNumLockOn)
    {
        UpdateNumLockUi();
    }

    private void UpdateNumLockUi()
    {
        var isNumLockOn = _numLockMonitor?.IsNumLockOn ?? false;
        var isMacroActive = _keyInterceptor?.IsMacroModeActive ?? false;
        KeyGrid.UpdateNumLockStatus(isNumLockOn, isMacroActive);
    }

    private void OnAutoSwitchChanged(object? sender, RoutedEventArgs e)
    {
        _appState?.SetAutoProfileSwitchEnabled(AutoSwitchHeaderCheckBox.IsChecked == true);
    }

    private void OnMacroEnabledChanged(object? sender, RoutedEventArgs e)
    {
        _appState?.SetMacroEnabled(MacroEnabledCheckBox.IsChecked == true);
        _keyInterceptor?.NotifyMacroState();
        UpdateNumLockUi();
    }

    private void OnStartOnBootChanged(object? sender, RoutedEventArgs e)
    {
        if (_appState != null)
            _appState.Settings.StartOnBoot = StartOnBootCheckBox.IsChecked == true;
    }

    private void OnStartMinimizedChanged(object? sender, RoutedEventArgs e)
    {
        if (_appState != null)
            _appState.Settings.StartMinimized = StartMinimizedCheckBox.IsChecked == true;
    }

    private void OnNumLockModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_appState != null && NumLockModeComboBox.SelectedItem is NumLockMacroMode mode)
        {
            _appState.Settings.NumLockMode = mode;
            _keyInterceptor?.NotifyMacroState();
            UpdateNumLockUi();
        }
    }

    private void OnHudModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_appState != null && HudModeComboBox.SelectedItem is HudVisibilityMode mode)
        {
            _appState.SetHudMode(mode);
        }
    }

    private void OnHudOpacitySliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty && sender is Slider slider)
        {
            HudOpacityText.Text = $"{(int)(slider.Value * 100)}%";
            _appState?.SetHudOpacity(slider.Value);
        }
    }

    private void OnOpacityDownClicked(object? sender, RoutedEventArgs e)
    {
        HudOpacitySlider.Value = Math.Max(0.20, HudOpacitySlider.Value - 0.05);
    }

    private void OnOpacityUpClicked(object? sender, RoutedEventArgs e)
    {
        HudOpacitySlider.Value = Math.Min(1.00, HudOpacitySlider.Value + 0.05);
    }

    private void OnLockHudChanged(object? sender, RoutedEventArgs e)
    {
        _appState?.SetHudLocked(LockHudCheckBox.IsChecked == true);
    }

    private void OnResetHudClicked(object? sender, RoutedEventArgs e)
    {
        _appState?.RequestResetHudPosition();
    }
}
