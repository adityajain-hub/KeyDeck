using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Avalonia.Views;

public partial class ActionEditorControl : UserControl
{
    private IProfileManager? _profileManager;
    private IActionExecutor? _actionExecutor;
    private NumpadKeyId _currentKeyId = NumpadKeyId.Numpad7;
    private KeyAction _currentAction = new();
    private bool _isUpdatingUi;

    public event Action<NumpadKeyId, KeyAction>? BindingModified;

    public ActionEditorControl()
    {
        InitializeComponent();
        PopulateDropdowns();
    }

    public void Initialize(IProfileManager profileManager, IActionExecutor actionExecutor)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));

        LoadKeyBinding(_currentKeyId);
    }

    private void PopulateDropdowns()
    {
        ActionTypeComboBox.ItemsSource = Enum.GetValues<ActionType>();
        MediaCommandComboBox.ItemsSource = Enum.GetValues<MediaCommand>();
        SystemCommandComboBox.ItemsSource = Enum.GetValues<SystemCommand>();
    }

    public void LoadKeyBinding(NumpadKeyId keyId)
    {
        _currentKeyId = keyId;
        KeyTitleText.Text = $"EDITING: {FormatKeyName(keyId)}";

        if (_profileManager == null) return;

        _currentAction = _profileManager.GetActiveAction(keyId).Clone();
        UpdateUiFromAction(_currentAction);
    }

    private static string FormatKeyName(NumpadKeyId keyId) => keyId switch
    {
        NumpadKeyId.Numpad0 => "NUMPAD 0",
        NumpadKeyId.Numpad1 => "NUMPAD 1",
        NumpadKeyId.Numpad2 => "NUMPAD 2",
        NumpadKeyId.Numpad3 => "NUMPAD 3",
        NumpadKeyId.Numpad4 => "NUMPAD 4",
        NumpadKeyId.Numpad5 => "NUMPAD 5",
        NumpadKeyId.Numpad6 => "NUMPAD 6",
        NumpadKeyId.Numpad7 => "NUMPAD 7",
        NumpadKeyId.Numpad8 => "NUMPAD 8",
        NumpadKeyId.Numpad9 => "NUMPAD 9",
        NumpadKeyId.NumpadDecimal => "NUMPAD . (DECIMAL)",
        NumpadKeyId.NumpadAdd => "NUMPAD + (ADD)",
        NumpadKeyId.NumpadSubtract => "NUMPAD - (SUBTRACT)",
        NumpadKeyId.NumpadMultiply => "NUMPAD * (MULTIPLY)",
        NumpadKeyId.NumpadDivide => "NUMPAD / (DIVIDE)",
        NumpadKeyId.NumpadEnter => "NUMPAD ENTER",
        _ => keyId.ToString().ToUpperInvariant()
    };

    private void UpdateUiFromAction(KeyAction action)
    {
        _isUpdatingUi = true;

        ActionTypeComboBox.SelectedItem = action.Type;
        LabelTextBox.Text = action.Label;
        PathTextBox.Text = action.TargetPath ?? string.Empty;
        ArgsTextBox.Text = action.Arguments ?? string.Empty;
        UrlTextBox.Text = action.TargetPath ?? string.Empty;
        HotkeyDisplayBox.Text = action.HotkeyDisplay ?? string.Empty;
        MediaCommandComboBox.SelectedItem = action.MediaCommand;
        SystemCommandComboBox.SelectedItem = action.SystemCommand;
        SnippetTextBox.Text = action.TextToPaste ?? string.Empty;

        if (_profileManager != null)
        {
            TargetProfileComboBox.ItemsSource = _profileManager.Profiles.Select(p => p.Name).ToList();
            var targetProfile = _profileManager.Profiles.FirstOrDefault(p => p.Id == action.TargetProfileId);
            TargetProfileComboBox.SelectedItem = targetProfile?.Name;
        }

        UpdateVisibility(action.Type);

        _isUpdatingUi = false;
    }

    private void UpdateVisibility(ActionType type)
    {
        PathParamsPanel.IsVisible = type is ActionType.LaunchApp or ActionType.RunScript or ActionType.OpenFolder;
        PathLabelText.Text = type switch
        {
            ActionType.OpenFolder => "Target Folder Path",
            ActionType.RunScript => "Script File (.sh, .py, .bash)",
            _ => "Target Executable or File"
        };

        UrlParamsPanel.IsVisible = type == ActionType.OpenUrl;
        HotkeyParamsPanel.IsVisible = type == ActionType.Hotkey;
        MediaParamsPanel.IsVisible = type == ActionType.MediaControl;
        SystemParamsPanel.IsVisible = type == ActionType.SystemAction;
        TextParamsPanel.IsVisible = type == ActionType.TextPaste;
        ProfileParamsPanel.IsVisible = type == ActionType.SwitchProfile;
    }

    private void OnActionTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;

        if (ActionTypeComboBox.SelectedItem is ActionType newType)
        {
            _currentAction.Type = newType;
            UpdateVisibility(newType);
            SaveCurrentAction();
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e) => OnFieldChanged();
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => OnFieldChanged();

    private void OnFieldChanged()
    {
        if (_isUpdatingUi) return;

        _currentAction.Label = LabelTextBox.Text ?? string.Empty;

        switch (_currentAction.Type)
        {
            case ActionType.LaunchApp:
            case ActionType.RunScript:
            case ActionType.OpenFolder:
                _currentAction.TargetPath = PathTextBox.Text;
                _currentAction.Arguments = ArgsTextBox.Text;
                break;

            case ActionType.OpenUrl:
                _currentAction.TargetPath = UrlTextBox.Text;
                break;

            case ActionType.MediaControl:
                if (MediaCommandComboBox.SelectedItem is MediaCommand mc)
                    _currentAction.MediaCommand = mc;
                break;

            case ActionType.SystemAction:
                if (SystemCommandComboBox.SelectedItem is SystemCommand sc)
                    _currentAction.SystemCommand = sc;
                break;

            case ActionType.TextPaste:
                _currentAction.TextToPaste = SnippetTextBox.Text;
                break;

            case ActionType.SwitchProfile:
                if (_profileManager != null && TargetProfileComboBox.SelectedItem is string name)
                {
                    var p = _profileManager.Profiles.FirstOrDefault(x => x.Name == name);
                    _currentAction.TargetProfileId = p?.Id;
                }
                break;
        }

        SaveCurrentAction();
    }

    private void SaveCurrentAction()
    {
        if (_profileManager != null)
        {
            _profileManager.UpdateBinding(_currentKeyId, _currentAction.Clone());
            BindingModified?.Invoke(_currentKeyId, _currentAction);
        }
    }

    private async void OnBrowseFileClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File or Executable",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            PathTextBox.Text = path;
            OnFieldChanged();
        }
    }

    private bool _isRecordingHotkey;

    private void OnRecordHotkeyClicked(object? sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
        {
            StopRecordingHotkey();
            return;
        }

        _isRecordingHotkey = true;
        RecordHotkeyButton.Content = "Listening...";
        HotkeyDisplayBox.Text = "Press keys on keyboard...";

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.KeyDown += OnRecordKeyDown;
        }
    }

    private void StopRecordingHotkey()
    {
        _isRecordingHotkey = false;
        RecordHotkeyButton.Content = "Record";
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.KeyDown -= OnRecordKeyDown;
        }
    }

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        // Skip lone modifier keys
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        e.Handled = true;

        var vks = new System.Collections.Generic.List<int>();
        var parts = new System.Collections.Generic.List<string>();

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vks.Add(0x11);
            parts.Add("Ctrl");
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            vks.Add(0x12);
            parts.Add("Alt");
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vks.Add(0x10);
            parts.Add("Shift");
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            vks.Add(0x5B);
            parts.Add("Win");
        }

        // Map primary key
        int vk = 0;
        if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            vk = 0x41 + (int)(e.Key - Key.A);
            parts.Add(e.Key.ToString());
        }
        else if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            vk = 0x30 + (int)(e.Key - Key.D0);
            parts.Add(((char)('0' + (e.Key - Key.D0))).ToString());
        }
        else if (e.Key >= Key.F1 && e.Key <= Key.F12)
        {
            vk = 0x70 + (int)(e.Key - Key.F1);
            parts.Add(e.Key.ToString());
        }
        else if (e.Key == Key.Space)
        {
            vk = 0x20;
            parts.Add("Space");
        }
        else if (e.Key == Key.Enter)
        {
            vk = 0x0D;
            parts.Add("Enter");
        }
        else if (e.Key == Key.Tab)
        {
            vk = 0x09;
            parts.Add("Tab");
        }
        else if (e.Key == Key.Escape)
        {
            vk = 0x1B;
            parts.Add("Esc");
        }

        if (vk != 0)
        {
            vks.Add(vk);
            var display = string.Join(" + ", parts);
            _currentAction.HotkeyVirtualKeys = vks;
            _currentAction.HotkeyDisplay = display;
            HotkeyDisplayBox.Text = display;
            SaveCurrentAction();
        }

        StopRecordingHotkey();
    }

    private async void OnTestActionClicked(object? sender, RoutedEventArgs e)
    {
        if (_actionExecutor != null && _currentAction.Type != ActionType.None)
        {
            await _actionExecutor.ExecuteAsync(_currentAction).ConfigureAwait(false);
        }
    }

    private void OnClearBindingClicked(object? sender, RoutedEventArgs e)
    {
        _currentAction = new KeyAction { Type = ActionType.None };
        UpdateUiFromAction(_currentAction);
        SaveCurrentAction();
    }
}
