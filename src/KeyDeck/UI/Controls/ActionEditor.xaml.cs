using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;

namespace KeyDeck.UI.Controls;

public partial class ActionEditor : UserControl
{
    private NumpadKeyId _currentKeyId;
    private KeyAction _currentAction = new();
    private string _iconsDirectory = string.Empty;
    private bool _isLoading;

    public KeyInterceptor? KeyInterceptor { get; set; }
    public IActionExecutor? ActionExecutor { get; set; }

    public event Action<NumpadKeyId, KeyAction>? ActionModified;

    public ActionEditor()
    {
        InitializeComponent();
        PopulateDropdowns();
    }

    private void PopulateDropdowns()
    {
        ActionTypeComboBox.ItemsSource = Enum.GetValues<ActionType>();
        MediaCommandComboBox.ItemsSource = Enum.GetValues<MediaCommand>();
        SystemCommandComboBox.ItemsSource = Enum.GetValues<SystemCommand>();
    }

    public void LoadAction(NumpadKeyId keyId, KeyAction action, string iconsDirectory)
    {
        _isLoading = true;
        _currentKeyId = keyId;
        _currentAction = action.Clone();
        _iconsDirectory = iconsDirectory;

        HeaderTitle.Text = $"CONFIGURE: {FormatKeyName(keyId).ToUpperInvariant()}";
        ActionTypeComboBox.SelectedItem = _currentAction.Type;
        LabelTextBox.Text = _currentAction.Label;

        // Populate fields
        AppPathTextBox.Text = _currentAction.TargetPath ?? string.Empty;
        AppArgsTextBox.Text = _currentAction.Arguments ?? string.Empty;
        UrlTextBox.Text = _currentAction.TargetPath ?? string.Empty;
        FolderPathTextBox.Text = _currentAction.TargetPath ?? string.Empty;
        HotkeyTextBox.Text = string.IsNullOrEmpty(_currentAction.HotkeyDisplay) ? "Click to record hotkey..." : _currentAction.HotkeyDisplay;
        ScriptPathTextBox.Text = _currentAction.TargetPath ?? string.Empty;
        ScriptArgsTextBox.Text = _currentAction.Arguments ?? string.Empty;
        MediaCommandComboBox.SelectedItem = _currentAction.MediaCommand;
        SystemCommandComboBox.SelectedItem = _currentAction.SystemCommand;
        TextPasteTextBox.Text = _currentAction.TextToPaste ?? string.Empty;

        UpdateSubActionsList();
        UpdateIconPreview();
        UpdatePanelVisibility(_currentAction.Type);

        _isLoading = false;
    }

    private void UpdatePanelVisibility(ActionType type)
    {
        LaunchAppPanel.Visibility = type == ActionType.LaunchApp ? Visibility.Visible : Visibility.Collapsed;
        OpenUrlPanel.Visibility = type == ActionType.OpenUrl ? Visibility.Visible : Visibility.Collapsed;
        OpenFolderPanel.Visibility = type == ActionType.OpenFolder ? Visibility.Visible : Visibility.Collapsed;
        HotkeyPanel.Visibility = type == ActionType.Hotkey ? Visibility.Visible : Visibility.Collapsed;
        RunScriptPanel.Visibility = type == ActionType.RunScript ? Visibility.Visible : Visibility.Collapsed;
        MediaControlPanel.Visibility = type == ActionType.MediaControl ? Visibility.Visible : Visibility.Collapsed;
        SystemActionPanel.Visibility = type == ActionType.SystemAction ? Visibility.Visible : Visibility.Collapsed;
        TextPastePanel.Visibility = type == ActionType.TextPaste ? Visibility.Visible : Visibility.Collapsed;
        MultiActionPanel.Visibility = type == ActionType.MultiAction ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateIconPreview()
    {
        if (!string.IsNullOrWhiteSpace(_currentAction.IconPath))
        {
            IconPreview.Source = IconExtractor.LoadFrozenBitmap(_currentAction.IconPath);
        }
        else if (_currentAction.Type == ActionType.LaunchApp && !string.IsNullOrWhiteSpace(_currentAction.TargetPath))
        {
            IconPreview.Source = IconExtractor.ExtractOrGetCachedIcon(_currentAction.TargetPath, _iconsDirectory);
        }
        else
        {
            IconPreview.Source = null;
        }
    }

    private void UpdateSubActionsList()
    {
        SubActionsListBox.ItemsSource = null;
        SubActionsListBox.ItemsSource = _currentAction.SubActions.Select(s => $"{s.Type}: {s.Label} ({s.DelayMs}ms)").ToList();
    }

    private void OnActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (ActionTypeComboBox.SelectedItem is ActionType newType)
        {
            _currentAction.Type = newType;
            UpdatePanelVisibility(newType);
            UpdateIconPreview();
            NotifyActionModified();
        }
    }

    private void OnInputChanged(object? sender, EventArgs? e)
    {
        if (_isLoading) return;

        _currentAction.Label = LabelTextBox.Text;

        switch (_currentAction.Type)
        {
            case ActionType.LaunchApp:
                _currentAction.TargetPath = AppPathTextBox.Text;
                _currentAction.Arguments = AppArgsTextBox.Text;
                break;
            case ActionType.OpenUrl:
                _currentAction.TargetPath = UrlTextBox.Text;
                break;
            case ActionType.OpenFolder:
                _currentAction.TargetPath = FolderPathTextBox.Text;
                break;
            case ActionType.RunScript:
                _currentAction.TargetPath = ScriptPathTextBox.Text;
                _currentAction.Arguments = ScriptArgsTextBox.Text;
                break;
            case ActionType.MediaControl:
                if (MediaCommandComboBox.SelectedItem is MediaCommand mc) _currentAction.MediaCommand = mc;
                break;
            case ActionType.SystemAction:
                if (SystemCommandComboBox.SelectedItem is SystemCommand sc) _currentAction.SystemCommand = sc;
                break;
            case ActionType.TextPaste:
                _currentAction.TextToPaste = TextPasteTextBox.Text;
                break;
        }

        UpdateIconPreview();
        NotifyActionModified();
    }

    private void OnBrowseAppClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = "Select Application"
        };

        if (dlg.ShowDialog() == true)
        {
            AppPathTextBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
            OnInputChanged(null, null);
        }
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        dialog.Description = "Select Folder to Open";
        dialog.UseDescriptionForTitle = true;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderPathTextBox.Text = dialog.SelectedPath;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = Path.GetFileName(dialog.SelectedPath);
            }
            OnInputChanged(null, null);
        }
    }

    private void OnBrowseScriptClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Scripts (*.ps1;*.bat;*.cmd)|*.ps1;*.bat;*.cmd|All Files (*.*)|*.*",
            Title = "Select Automation Script"
        };

        if (dlg.ShowDialog() == true)
        {
            ScriptPathTextBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
            OnInputChanged(null, null);
        }
    }

    private void OnBrowseIconClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Icons & Images (*.ico;*.png;*.jpg;*.exe)|*.ico;*.png;*.jpg;*.exe|All Files (*.*)|*.*",
            Title = "Select Icon"
        };

        if (dlg.ShowDialog() == true)
        {
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (ext == ".exe")
            {
                _currentAction.IconPath = null;
                IconExtractor.ExtractOrGetCachedIcon(dlg.FileName, _iconsDirectory);
            }
            else
            {
                _currentAction.IconPath = dlg.FileName;
            }

            UpdateIconPreview();
            NotifyActionModified();
        }
    }

    private void OnResetIconClick(object sender, RoutedEventArgs e)
    {
        _currentAction.IconPath = null;
        UpdateIconPreview();
        NotifyActionModified();
    }

    private void OnHotkeyGotFocus(object sender, RoutedEventArgs e)
    {
        if (KeyInterceptor != null)
        {
            KeyInterceptor.IsTemporaryBypassEnabled = true;
        }
        HotkeyTextBox.Text = "Press keys now...";
    }

    private void OnHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        if (KeyInterceptor != null)
        {
            KeyInterceptor.IsTemporaryBypassEnabled = false;
        }
        if (string.IsNullOrEmpty(_currentAction.HotkeyDisplay))
        {
            HotkeyTextBox.Text = "Click to record hotkey...";
        }
        else
        {
            HotkeyTextBox.Text = _currentAction.HotkeyDisplay;
        }
    }

    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var result = HotkeyRecorder.ProcessKeyDown(e);
        if (result.HasValue)
        {
            _currentAction.HotkeyVirtualKeys = result.Value.virtualKeys;
            _currentAction.HotkeyDisplay = result.Value.displayText;
            HotkeyTextBox.Text = result.Value.displayText;

            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = result.Value.displayText;
            }

            NotifyActionModified();
        }
    }

    private void OnClearHotkeyClick(object sender, RoutedEventArgs e)
    {
        _currentAction.HotkeyVirtualKeys.Clear();
        _currentAction.HotkeyDisplay = null;
        HotkeyTextBox.Text = "Click to record hotkey...";
        NotifyActionModified();
    }

    private void OnAddSubActionClick(object sender, RoutedEventArgs e)
    {
        var subAction = new KeyAction
        {
            Type = ActionType.LaunchApp,
            Label = "Sub-action",
            DelayMs = 150
        };

        _currentAction.SubActions.Add(subAction);
        UpdateSubActionsList();
        NotifyActionModified();
    }

    private void OnRemoveSubActionClick(object sender, RoutedEventArgs e)
    {
        var index = SubActionsListBox.SelectedIndex;
        if (index >= 0 && index < _currentAction.SubActions.Count)
        {
            _currentAction.SubActions.RemoveAt(index);
            UpdateSubActionsList();
            NotifyActionModified();
        }
    }

    private void OnTestActionClick(object sender, RoutedEventArgs e)
    {
        var executor = ActionExecutor ?? new ActionExecutor();
        _ = executor.ExecuteAsync(_currentAction);
    }

    private void OnClearKeyClick(object sender, RoutedEventArgs e)
    {
        _currentAction = new KeyAction { Type = ActionType.None };
        LoadAction(_currentKeyId, _currentAction, _iconsDirectory);
        NotifyActionModified();
    }

    private void NotifyActionModified()
    {
        ActionModified?.Invoke(_currentKeyId, _currentAction);
    }

    private static string FormatKeyName(NumpadKeyId keyId)
    {
        return keyId switch
        {
            NumpadKeyId.NumpadDecimal => "Decimal (.)",
            NumpadKeyId.NumpadAdd => "Add (+)",
            NumpadKeyId.NumpadSubtract => "Subtract (-)",
            NumpadKeyId.NumpadMultiply => "Multiply (*)",
            NumpadKeyId.NumpadDivide => "Divide (/)",
            NumpadKeyId.NumpadEnter => "Enter",
            _ => keyId.ToString()
        };
    }
}
