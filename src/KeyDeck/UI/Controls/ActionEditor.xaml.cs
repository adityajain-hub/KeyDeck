using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;

namespace KeyDeck.UI.Controls;

public partial class ActionEditor : UserControl
{
    private static readonly ActionType[] AllowedSubActionTypes =
    {
        ActionType.LaunchApp,
        ActionType.OpenUrl,
        ActionType.OpenFolder,
        ActionType.Hotkey,
        ActionType.RunScript,
        ActionType.MediaControl,
        ActionType.SystemAction,
        ActionType.TextPaste,
        ActionType.SwitchProfile
    };

    private readonly DispatcherTimer _saveDebounceTimer;
    private bool _hasPendingSave;
    private NumpadKeyId _currentKeyId;
    private KeyAction _currentAction = new();
    private string _iconsDirectory = string.Empty;
    private bool _isLoading;
    private bool _isSubActionLoading;

    public KeyDeck.Core.Abstractions.IKeyInterceptor? KeyInterceptor { get; set; }
    public IActionExecutor? ActionExecutor { get; set; }
    public IProfileManager? ProfileManager { get; set; }

    public event Action<NumpadKeyId, KeyAction>? ActionModified;

    public ActionEditor()
    {
        InitializeComponent();

        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _saveDebounceTimer.Tick += OnSaveDebounceTimerTick;

        PopulateDropdowns();
    }

    private void PopulateDropdowns()
    {
        ActionTypeComboBox.ItemsSource    = Enum.GetValues<ActionType>();
        MediaCommandComboBox.ItemsSource  = Enum.GetValues<MediaCommand>();
        SystemCommandComboBox.ItemsSource = Enum.GetValues<SystemCommand>();

        SubActionTypeComboBox.ItemsSource   = AllowedSubActionTypes;
        SubActionMediaComboBox.ItemsSource  = Enum.GetValues<MediaCommand>();
        SubActionSystemComboBox.ItemsSource = Enum.GetValues<SystemCommand>();
    }

    public void RefreshProfilesDropdown()
    {
        if (ProfileManager == null) return;

        var profiles = ProfileManager.Profiles;
        TargetProfileComboBox.ItemsSource    = null;
        TargetProfileComboBox.ItemsSource    = profiles;
        SubActionProfileComboBox.ItemsSource = null;
        SubActionProfileComboBox.ItemsSource = profiles;

        if (_currentAction.Type == ActionType.SwitchProfile)
        {
            TargetProfileComboBox.SelectedValue = _currentAction.TargetProfileId;
        }
    }

    public void FlushPendingSave()
    {
        if (_hasPendingSave)
        {
            _saveDebounceTimer.Stop();
            _hasPendingSave = false;
            UpdateIconPreview();
            ActionModified?.Invoke(_currentKeyId, _currentAction.Clone());
        }
    }

    private void OnSaveDebounceTimerTick(object? sender, EventArgs e)
    {
        FlushPendingSave();
    }

    public void LoadAction(NumpadKeyId keyId, KeyAction action, string iconsDirectory)
    {
        FlushPendingSave();

        _isLoading = true;
        _currentKeyId = keyId;
        _currentAction = action.Clone();
        _iconsDirectory = iconsDirectory;

        RefreshProfilesDropdown();

        HeaderTitle.Text = $"CONFIGURE: {FormatKeyName(keyId).ToUpperInvariant()}";
        ActionTypeComboBox.SelectedItem = _currentAction.Type;
        LabelTextBox.Text = _currentAction.Label;

        AppPathTextBox.Text    = _currentAction.TargetPath ?? string.Empty;
        AppArgsTextBox.Text    = _currentAction.Arguments ?? string.Empty;
        UrlTextBox.Text        = _currentAction.TargetPath ?? string.Empty;
        FolderPathTextBox.Text = _currentAction.TargetPath ?? string.Empty;
        HotkeyTextBox.Text     = string.IsNullOrEmpty(_currentAction.HotkeyDisplay) ? "Click to record hotkey..." : _currentAction.HotkeyDisplay;
        ScriptPathTextBox.Text = _currentAction.TargetPath ?? string.Empty;
        ScriptArgsTextBox.Text = _currentAction.Arguments ?? string.Empty;
        MediaCommandComboBox.SelectedItem  = _currentAction.MediaCommand;
        SystemCommandComboBox.SelectedItem = _currentAction.SystemCommand;
        TextPasteTextBox.Text              = _currentAction.TextToPaste ?? string.Empty;
        TargetProfileComboBox.SelectedValue = _currentAction.TargetProfileId;

        UpdateSubActionsList(preserveSelection: false);
        UpdateIconPreview();
        UpdatePanelVisibility(_currentAction.Type);

        _isLoading = false;
    }

    private void UpdatePanelVisibility(ActionType type)
    {
        LaunchAppPanel.Visibility     = type == ActionType.LaunchApp     ? Visibility.Visible : Visibility.Collapsed;
        OpenUrlPanel.Visibility       = type == ActionType.OpenUrl       ? Visibility.Visible : Visibility.Collapsed;
        OpenFolderPanel.Visibility    = type == ActionType.OpenFolder    ? Visibility.Visible : Visibility.Collapsed;
        HotkeyPanel.Visibility        = type == ActionType.Hotkey        ? Visibility.Visible : Visibility.Collapsed;
        RunScriptPanel.Visibility     = type == ActionType.RunScript     ? Visibility.Visible : Visibility.Collapsed;
        MediaControlPanel.Visibility  = type == ActionType.MediaControl  ? Visibility.Visible : Visibility.Collapsed;
        SystemActionPanel.Visibility  = type == ActionType.SystemAction  ? Visibility.Visible : Visibility.Collapsed;
        TextPastePanel.Visibility     = type == ActionType.TextPaste     ? Visibility.Visible : Visibility.Collapsed;
        MultiActionPanel.Visibility   = type == ActionType.MultiAction   ? Visibility.Visible : Visibility.Collapsed;
        SwitchProfilePanel.Visibility = type == ActionType.SwitchProfile ? Visibility.Visible : Visibility.Collapsed;
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

    // ── Top-level action changes ───────────────────────────────────────────

    private void OnActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (ActionTypeComboBox.SelectedItem is ActionType newType)
        {
            _currentAction.Type = newType;
            UpdatePanelVisibility(newType);
            UpdateIconPreview();
            NotifyActionModifiedImmediate();
        }
    }

    private void OnTargetProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (TargetProfileComboBox.SelectedItem is Profile targetProfile)
        {
            _currentAction.TargetProfileId = targetProfile.Id;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                _isLoading = true;
                LabelTextBox.Text = $"→ {targetProfile.Name}";
                _currentAction.Label = LabelTextBox.Text;
                _isLoading = false;
            }
            NotifyActionModifiedImmediate();
        }
    }

    private void SyncCurrentActionFromControls()
    {
        _currentAction.Label = LabelTextBox.Text;

        switch (_currentAction.Type)
        {
            case ActionType.LaunchApp:
                _currentAction.TargetPath = AppPathTextBox.Text;
                _currentAction.Arguments  = AppArgsTextBox.Text;
                break;
            case ActionType.OpenUrl:
                _currentAction.TargetPath = UrlTextBox.Text;
                break;
            case ActionType.OpenFolder:
                _currentAction.TargetPath = FolderPathTextBox.Text;
                break;
            case ActionType.RunScript:
                _currentAction.TargetPath = ScriptPathTextBox.Text;
                _currentAction.Arguments  = ScriptArgsTextBox.Text;
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
            case ActionType.SwitchProfile:
                if (TargetProfileComboBox.SelectedValue is string targetId) _currentAction.TargetProfileId = targetId;
                break;
        }
    }

    private void OnInputChanged(object? sender, EventArgs? e)
    {
        if (_isLoading) return;

        SyncCurrentActionFromControls();
        ScheduleDebouncedSave();
    }

    private void ScheduleDebouncedSave()
    {
        _hasPendingSave = true;
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void NotifyActionModifiedImmediate()
    {
        _saveDebounceTimer.Stop();
        _hasPendingSave = false;
        UpdateIconPreview();
        ActionModified?.Invoke(_currentKeyId, _currentAction.Clone());
    }

    // ── MultiAction Sub-Actions List & Inline Editor ───────────────────────

    private void UpdateSubActionsList(bool preserveSelection = true)
    {
        var prevIndex = preserveSelection ? SubActionsListBox.SelectedIndex : -1;

        SubActionsListBox.ItemsSource = null;
        SubActionsListBox.ItemsSource = _currentAction.SubActions
            .Select((s, idx) => $"{idx + 1}. [{s.Type}] {FormatSubActionSummary(s)} ({s.DelayMs}ms)")
            .ToList();

        if (_currentAction.SubActions.Count > 0)
        {
            SubActionsListBox.SelectedIndex = prevIndex >= 0 && prevIndex < _currentAction.SubActions.Count
                ? prevIndex
                : 0;
        }
        else
        {
            SubActionEditorCard.Visibility = Visibility.Collapsed;
        }

        UpdateSubActionButtonsState();
    }

    private static string FormatSubActionSummary(KeyAction sub)
    {
        if (!string.IsNullOrWhiteSpace(sub.Label) && sub.Label != "Sub-action")
        {
            return sub.Label;
        }

        return sub.Type switch
        {
            ActionType.LaunchApp or ActionType.OpenUrl or ActionType.OpenFolder or ActionType.RunScript
                => string.IsNullOrWhiteSpace(sub.TargetPath) ? "(No path)" : Path.GetFileName(sub.TargetPath),
            ActionType.Hotkey        => string.IsNullOrWhiteSpace(sub.HotkeyDisplay) ? "(No shortcut)" : sub.HotkeyDisplay,
            ActionType.MediaControl  => sub.MediaCommand.ToString(),
            ActionType.SystemAction  => sub.SystemCommand.ToString(),
            ActionType.TextPaste     => string.IsNullOrWhiteSpace(sub.TextToPaste) ? "(Empty text)" : sub.TextToPaste.Replace("\r", "").Replace("\n", " "),
            ActionType.SwitchProfile => string.IsNullOrWhiteSpace(sub.TargetProfileId) ? "(No profile)" : sub.TargetProfileId,
            _                        => sub.Label
        };
    }

    private void UpdateSubActionButtonsState()
    {
        var idx   = SubActionsListBox.SelectedIndex;
        var count = _currentAction.SubActions.Count;

        RemoveSubActionButton.IsEnabled   = idx >= 0 && idx < count;
        MoveSubActionUpButton.IsEnabled   = idx > 0;
        MoveSubActionDownButton.IsEnabled = idx >= 0 && idx < count - 1;
    }

    private void OnSubActionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSubActionButtonsState();

        var idx = SubActionsListBox.SelectedIndex;
        if (idx < 0 || idx >= _currentAction.SubActions.Count)
        {
            SubActionEditorCard.Visibility = Visibility.Collapsed;
            return;
        }

        var sub = _currentAction.SubActions[idx];
        _isSubActionLoading = true;

        SubActionEditorCard.Visibility     = Visibility.Visible;
        SubActionEditorHeader.Text         = $"EDIT STEP #{idx + 1}";
        SubActionTypeComboBox.SelectedItem = AllowedSubActionTypes.Contains(sub.Type) ? sub.Type : ActionType.LaunchApp;
        SubActionDelayTextBox.Text         = sub.DelayMs.ToString();
        SubActionLabelTextBox.Text         = sub.Label;
        SubActionTargetTextBox.Text        = sub.TargetPath ?? string.Empty;
        SubActionArgsTextBox.Text          = sub.Arguments ?? string.Empty;
        SubActionHotkeyTextBox.Text        = string.IsNullOrEmpty(sub.HotkeyDisplay) ? "Click & press shortcut..." : sub.HotkeyDisplay;
        SubActionMediaComboBox.SelectedItem  = sub.MediaCommand;
        SubActionSystemComboBox.SelectedItem = sub.SystemCommand;
        SubActionTextPasteBox.Text           = sub.TextToPaste ?? string.Empty;
        SubActionProfileComboBox.SelectedValue = sub.TargetProfileId;

        UpdateSubActionRowsVisibility(sub.Type);

        _isSubActionLoading = false;
    }

    private void UpdateSubActionRowsVisibility(ActionType subType)
    {
        var usesTarget = subType is ActionType.LaunchApp or ActionType.OpenUrl or ActionType.OpenFolder or ActionType.RunScript;
        SubActionTargetRow.Visibility        = usesTarget ? Visibility.Visible : Visibility.Collapsed;
        SubActionBrowseButton.Visibility     = subType is ActionType.LaunchApp or ActionType.OpenFolder or ActionType.RunScript ? Visibility.Visible : Visibility.Collapsed;
        SubActionTargetHeader.Text           = subType switch
        {
            ActionType.LaunchApp  => "Application Path (.exe)",
            ActionType.OpenUrl    => "Webpage URL",
            ActionType.OpenFolder => "Folder Path",
            ActionType.RunScript  => "Script Path (.ps1 / .bat)",
            _                     => "Target"
        };

        SubActionArgsRow.Visibility          = subType is ActionType.LaunchApp or ActionType.RunScript ? Visibility.Visible : Visibility.Collapsed;
        SubActionHotkeyRow.Visibility        = subType == ActionType.Hotkey        ? Visibility.Visible : Visibility.Collapsed;
        SubActionMediaRow.Visibility         = subType == ActionType.MediaControl  ? Visibility.Visible : Visibility.Collapsed;
        SubActionSystemRow.Visibility        = subType == ActionType.SystemAction  ? Visibility.Visible : Visibility.Collapsed;
        SubActionTextPasteRow.Visibility     = subType == ActionType.TextPaste     ? Visibility.Visible : Visibility.Collapsed;
        SubActionSwitchProfileRow.Visibility = subType == ActionType.SwitchProfile ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSubActionFieldChanged(object? sender, EventArgs? e)
    {
        if (_isSubActionLoading || _isLoading) return;

        var idx = SubActionsListBox.SelectedIndex;
        if (idx < 0 || idx >= _currentAction.SubActions.Count) return;

        var sub = _currentAction.SubActions[idx];

        if (SubActionTypeComboBox.SelectedItem is ActionType newSubType)
        {
            sub.Type = newSubType;
            UpdateSubActionRowsVisibility(newSubType);
        }

        if (int.TryParse(SubActionDelayTextBox.Text.Trim(), out var delayMs) && delayMs >= 0)
        {
            sub.DelayMs = Math.Min(delayMs, 60000);
        }

        sub.Label = SubActionLabelTextBox.Text;

        switch (sub.Type)
        {
            case ActionType.LaunchApp:
            case ActionType.OpenUrl:
            case ActionType.OpenFolder:
            case ActionType.RunScript:
                sub.TargetPath = SubActionTargetTextBox.Text;
                sub.Arguments  = SubActionArgsTextBox.Text;
                break;
            case ActionType.MediaControl:
                if (SubActionMediaComboBox.SelectedItem is MediaCommand mc) sub.MediaCommand = mc;
                break;
            case ActionType.SystemAction:
                if (SubActionSystemComboBox.SelectedItem is SystemCommand sc) sub.SystemCommand = sc;
                break;
            case ActionType.TextPaste:
                sub.TextToPaste = SubActionTextPasteBox.Text;
                break;
            case ActionType.SwitchProfile:
                if (SubActionProfileComboBox.SelectedItem is Profile prof) sub.TargetProfileId = prof.Id;
                break;
        }

        RefreshSubActionListItemText(idx);
        ScheduleDebouncedSave();
    }

    private void RefreshSubActionListItemText(int idx)
    {
        _isSubActionLoading = true;
        SubActionsListBox.ItemsSource = _currentAction.SubActions
            .Select((s, i) => $"{i + 1}. [{s.Type}] {FormatSubActionSummary(s)} ({s.DelayMs}ms)")
            .ToList();
        SubActionsListBox.SelectedIndex = idx;
        _isSubActionLoading = false;
    }

    private void OnSubActionBrowseClick(object sender, RoutedEventArgs e)
    {
        var idx = SubActionsListBox.SelectedIndex;
        if (idx < 0 || idx >= _currentAction.SubActions.Count) return;

        var sub = _currentAction.SubActions[idx];
        if (sub.Type == ActionType.OpenFolder)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Folder for Sub-Action",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SubActionTargetTextBox.Text = dialog.SelectedPath;
                if (string.IsNullOrWhiteSpace(SubActionLabelTextBox.Text) || SubActionLabelTextBox.Text == "Sub-action")
                {
                    SubActionLabelTextBox.Text = Path.GetFileName(dialog.SelectedPath);
                }
            }
        }
        else
        {
            var dlg = new OpenFileDialog
            {
                Filter = sub.Type == ActionType.RunScript
                    ? "Scripts (*.ps1;*.bat;*.cmd)|*.ps1;*.bat;*.cmd|All Files (*.*)|*.*"
                    : "Applications (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Target for Sub-Action"
            };
            if (dlg.ShowDialog() == true)
            {
                SubActionTargetTextBox.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(SubActionLabelTextBox.Text) || SubActionLabelTextBox.Text == "Sub-action")
                {
                    SubActionLabelTextBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }
    }

    private void OnSubActionHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var idx = SubActionsListBox.SelectedIndex;
        if (idx < 0 || idx >= _currentAction.SubActions.Count) return;

        var result = HotkeyRecorder.ProcessKeyDown(e);
        if (result.HasValue)
        {
            var sub = _currentAction.SubActions[idx];
            sub.HotkeyVirtualKeys = result.Value.virtualKeys;
            sub.HotkeyDisplay     = result.Value.displayText;
            SubActionHotkeyTextBox.Text = result.Value.displayText;

            if (string.IsNullOrWhiteSpace(SubActionLabelTextBox.Text) || SubActionLabelTextBox.Text == "Sub-action")
            {
                SubActionLabelTextBox.Text = result.Value.displayText;
            }

            RefreshSubActionListItemText(idx);
            NotifyActionModifiedImmediate();
        }
    }

    private void OnSubActionHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        if (KeyInterceptor != null)
        {
            KeyInterceptor.IsTemporaryBypassEnabled = false;
        }

        var idx = SubActionsListBox.SelectedIndex;
        if (idx >= 0 && idx < _currentAction.SubActions.Count)
        {
            var sub = _currentAction.SubActions[idx];
            SubActionHotkeyTextBox.Text = string.IsNullOrEmpty(sub.HotkeyDisplay)
                ? "Click & press shortcut..."
                : sub.HotkeyDisplay;
        }
    }

    private void OnAddSubActionClick(object sender, RoutedEventArgs e)
    {
        var subAction = new KeyAction
        {
            Type    = ActionType.Hotkey,
            Label   = $"Step {_currentAction.SubActions.Count + 1}",
            DelayMs = 150
        };

        _currentAction.SubActions.Add(subAction);
        UpdateSubActionsList(preserveSelection: false);
        SubActionsListBox.SelectedIndex = _currentAction.SubActions.Count - 1;
        NotifyActionModifiedImmediate();
    }

    private void OnMoveSubActionUpClick(object sender, RoutedEventArgs e)
    {
        var idx = SubActionsListBox.SelectedIndex;
        if (idx <= 0 || idx >= _currentAction.SubActions.Count) return;

        (_currentAction.SubActions[idx - 1], _currentAction.SubActions[idx]) =
            (_currentAction.SubActions[idx], _currentAction.SubActions[idx - 1]);

        UpdateSubActionsList(preserveSelection: false);
        SubActionsListBox.SelectedIndex = idx - 1;
        NotifyActionModifiedImmediate();
    }

    private void OnMoveSubActionDownClick(object sender, RoutedEventArgs e)
    {
        var idx = SubActionsListBox.SelectedIndex;
        if (idx < 0 || idx >= _currentAction.SubActions.Count - 1) return;

        (_currentAction.SubActions[idx + 1], _currentAction.SubActions[idx]) =
            (_currentAction.SubActions[idx], _currentAction.SubActions[idx + 1]);

        UpdateSubActionsList(preserveSelection: false);
        SubActionsListBox.SelectedIndex = idx + 1;
        NotifyActionModifiedImmediate();
    }

    private void OnRemoveSubActionClick(object sender, RoutedEventArgs e)
    {
        var index = SubActionsListBox.SelectedIndex;
        if (index >= 0 && index < _currentAction.SubActions.Count)
        {
            _currentAction.SubActions.RemoveAt(index);
            UpdateSubActionsList(preserveSelection: true);
            NotifyActionModifiedImmediate();
        }
    }

    // ── File / Folder / Icon Browsers ──────────────────────────────────────

    private void OnBrowseAppClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = "Select Application"
        };

        if (dlg.ShowDialog() == true)
        {
            _isLoading = true;
            AppPathTextBox.Text = dlg.FileName;
            _currentAction.TargetPath = dlg.FileName;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                _currentAction.Label = LabelTextBox.Text;
            }
            _isLoading = false;
            NotifyActionModifiedImmediate();
        }
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Folder to Open",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _isLoading = true;
            FolderPathTextBox.Text = dialog.SelectedPath;
            _currentAction.TargetPath = dialog.SelectedPath;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = Path.GetFileName(dialog.SelectedPath);
                _currentAction.Label = LabelTextBox.Text;
            }
            _isLoading = false;
            NotifyActionModifiedImmediate();
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
            _isLoading = true;
            ScriptPathTextBox.Text = dlg.FileName;
            _currentAction.TargetPath = dlg.FileName;
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                LabelTextBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                _currentAction.Label = LabelTextBox.Text;
            }
            _isLoading = false;
            NotifyActionModifiedImmediate();
        }
    }

    private void OnBrowseIconClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Icons & Executables (*.ico;*.png;*.jpg;*.exe)|*.ico;*.png;*.jpg;*.exe|All Files (*.*)|*.*",
            Title = "Select Custom Icon or Executable"
        };

        if (dlg.ShowDialog() == true)
        {
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (ext == ".exe")
            {
                // Extract the executable's icon to the PNG cache and persist that PNG path in IconPath
                // so any action type (Hotkey, RunScript, SwitchProfile, LaunchApp) can borrow an .exe icon.
                _currentAction.IconPath = IconExtractor.ExtractIconToCacheFile(dlg.FileName, _iconsDirectory);
            }
            else
            {
                _currentAction.IconPath = dlg.FileName;
            }

            NotifyActionModifiedImmediate();
        }
    }

    private void OnResetIconClick(object sender, RoutedEventArgs e)
    {
        _currentAction.IconPath = null;
        NotifyActionModifiedImmediate();
    }

    // ── Hotkey Recorder ────────────────────────────────────────────────────

    private void OnHotkeyGotFocus(object sender, RoutedEventArgs e)
    {
        if (KeyInterceptor != null)
        {
            KeyInterceptor.IsTemporaryBypassEnabled = true;
        }
        if (sender is TextBox tb)
        {
            tb.Text = "Press keys now...";
        }
    }

    private void OnHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        if (KeyInterceptor != null)
        {
            KeyInterceptor.IsTemporaryBypassEnabled = false;
        }
        HotkeyTextBox.Text = string.IsNullOrEmpty(_currentAction.HotkeyDisplay)
            ? "Click to record hotkey..."
            : _currentAction.HotkeyDisplay;
    }

    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var result = HotkeyRecorder.ProcessKeyDown(e);
        if (result.HasValue)
        {
            _currentAction.HotkeyVirtualKeys = result.Value.virtualKeys;
            _currentAction.HotkeyDisplay     = result.Value.displayText;
            HotkeyTextBox.Text               = result.Value.displayText;

            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                _isLoading = true;
                LabelTextBox.Text    = result.Value.displayText;
                _currentAction.Label = LabelTextBox.Text;
                _isLoading = false;
            }

            NotifyActionModifiedImmediate();
        }
    }

    private void OnClearHotkeyClick(object sender, RoutedEventArgs e)
    {
        _currentAction.HotkeyVirtualKeys.Clear();
        _currentAction.HotkeyDisplay = null;
        HotkeyTextBox.Text = "Click to record hotkey...";
        NotifyActionModifiedImmediate();
    }

    // ── Test & Clear ───────────────────────────────────────────────────────

    private async void OnTestActionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            SyncCurrentActionFromControls();
            FlushPendingSave();

            if (_currentAction.Type == ActionType.None)
            {
                MessageBox.Show("Please select an Action Type first before testing.", "KeyDeck", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_currentAction.Type == ActionType.OpenUrl && string.IsNullOrWhiteSpace(_currentAction.TargetPath))
            {
                MessageBox.Show("Please enter a valid Webpage URL before testing.", "KeyDeck", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentAction.Type == ActionType.LaunchApp && string.IsNullOrWhiteSpace(_currentAction.TargetPath))
            {
                MessageBox.Show("Please select or enter an Application Executable path before testing.", "KeyDeck", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var origContent = TestActionButton.Content;
            TestActionButton.Content = "⏳ Testing...";
            TestActionButton.IsEnabled = false;

            try
            {
                var executor = ActionExecutor ?? new ActionExecutor(ProfileManager);
                await executor.ExecuteAsync(_currentAction);
            }
            finally
            {
                await Task.Delay(350);
                TestActionButton.Content = origContent;
                TestActionButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to execute test action: {ex.Message}", "KeyDeck Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClearKeyClick(object sender, RoutedEventArgs e)
    {
        _saveDebounceTimer.Stop();
        _hasPendingSave = false;
        _currentAction = new KeyAction { Type = ActionType.None };
        LoadAction(_currentKeyId, _currentAction, _iconsDirectory);
        NotifyActionModifiedImmediate();
    }

    private static string FormatKeyName(NumpadKeyId keyId)
    {
        return keyId switch
        {
            NumpadKeyId.NumpadDecimal  => "Decimal (.)",
            NumpadKeyId.NumpadAdd      => "Add (+)",
            NumpadKeyId.NumpadSubtract => "Subtract (-)",
            NumpadKeyId.NumpadMultiply => "Multiply (*)",
            NumpadKeyId.NumpadDivide   => "Divide (/)",
            NumpadKeyId.NumpadEnter    => "Enter",
            _                          => keyId.ToString()
        };
    }
}
