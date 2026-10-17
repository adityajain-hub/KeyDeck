using System.Windows.Input;
using System.Windows.Media;
using KeyDeck.Core;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Controls;

public partial class NumpadGrid : UserControl
{
    private readonly Dictionary<NumpadKeyId, NumpadKeyCell> _cells = new();
    private NumpadKeyId _selectedKeyId = NumpadKeyId.Numpad7;
    private NumpadKeyId? _activatedByFirstClick;

    public NumpadKeyId SelectedKeyId => _selectedKeyId;

    public event Action<NumpadKeyId>? KeyCellSelected;
    public event Action<NumpadKeyId>? KeyCellActivated;
    public event Action<NumpadKeyId, string>? FileDroppedOnKey;
    public event Action? NumLockCellClicked;

    public NumpadGrid()
    {
        InitializeComponent();
        RegisterCells();
    }

    private void RegisterCells()
    {
        _cells[NumpadKeyId.Numpad0] = Cell0;
        _cells[NumpadKeyId.Numpad1] = Cell1;
        _cells[NumpadKeyId.Numpad2] = Cell2;
        _cells[NumpadKeyId.Numpad3] = Cell3;
        _cells[NumpadKeyId.Numpad4] = Cell4;
        _cells[NumpadKeyId.Numpad5] = Cell5;
        _cells[NumpadKeyId.Numpad6] = Cell6;
        _cells[NumpadKeyId.Numpad7] = Cell7;
        _cells[NumpadKeyId.Numpad8] = Cell8;
        _cells[NumpadKeyId.Numpad9] = Cell9;
        _cells[NumpadKeyId.NumpadDecimal] = CellDecimal;
        _cells[NumpadKeyId.NumpadAdd] = CellAdd;
        _cells[NumpadKeyId.NumpadSubtract] = CellSubtract;
        _cells[NumpadKeyId.NumpadMultiply] = CellMultiply;
        _cells[NumpadKeyId.NumpadDivide] = CellDivide;
        _cells[NumpadKeyId.NumpadEnter] = CellEnter;

        foreach (var kvp in _cells)
        {
            var keyId = kvp.Key;
            var cell = kvp.Value;
            cell.KeyId = keyId;
            cell.CellClicked += OnCellClicked;
            cell.CellDoubleClicked += OnCellDoubleClicked;
            cell.FileDropped += (id, file) => FileDroppedOnKey?.Invoke(id, file);
        }

        SelectKey(NumpadKeyId.Numpad7);
    }

    private void OnCellClicked(NumpadKeyCell cell)
    {
        if (_selectedKeyId == cell.KeyId)
        {
            _activatedByFirstClick = cell.KeyId;
            // Key is already selected: clicking it again executes its action immediately!
            KeyCellActivated?.Invoke(cell.KeyId);
            FlashKey(cell.KeyId);
        }
        else
        {
            _activatedByFirstClick = null;
            SelectKey(cell.KeyId);
            KeyCellSelected?.Invoke(cell.KeyId);
        }
    }

    private void OnCellDoubleClicked(NumpadKeyCell cell)
    {
        if (_activatedByFirstClick == cell.KeyId)
        {
            _activatedByFirstClick = null;
            return;
        }

        SelectKey(cell.KeyId);
        KeyCellSelected?.Invoke(cell.KeyId);
        KeyCellActivated?.Invoke(cell.KeyId);
        FlashKey(cell.KeyId);
    }

    public void SelectKey(NumpadKeyId keyId)
    {
        _selectedKeyId = keyId;

        foreach (var kvp in _cells)
        {
            kvp.Value.IsSelected = kvp.Key == keyId;
        }
    }

    public void FlashKey(NumpadKeyId keyId)
    {
        if (_cells.TryGetValue(keyId, out var cell))
        {
            cell.FlashKeyPress();
        }
    }

    public void UpdateBindings(Profile profile, string iconsDirectory)
    {
        foreach (var kvp in _cells)
        {
            var keyId = kvp.Key;
            var cell = kvp.Value;
            var action = profile.GetAction(keyId);

            cell.ActionLabel = string.IsNullOrWhiteSpace(action.Label) ? "(Unassigned)" : action.Label;

            // Load icon
            if (!string.IsNullOrWhiteSpace(action.IconPath))
            {
                var icon = IconExtractor.LoadFrozenBitmap(action.IconPath);
                cell.SetIcon(icon);
            }
            else if (action.Type == ActionType.LaunchApp && !string.IsNullOrWhiteSpace(action.TargetPath))
            {
                var icon = IconExtractor.ExtractOrGetCachedIcon(action.TargetPath, iconsDirectory);
                cell.SetIcon(icon);
            }
            else
            {
                cell.SetIcon(null);
            }
        }
    }

    public void UpdateNumLockStatus(bool isMacroActive)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (isMacroActive)
            {
                NumLockIndicatorDot.Fill = (Brush)FindResource("AccentBrush");
                NumLockStatusLabel.Text = "MACROS ACTIVE";
                NumLockStatusLabel.Foreground = (Brush)FindResource("AccentBrush");
            }
            else
            {
                NumLockIndicatorDot.Fill = (Brush)FindResource("SecondaryTextBrush");
                NumLockStatusLabel.Text = "NUMPAD TYPING";
                NumLockStatusLabel.Foreground = (Brush)FindResource("SecondaryTextBrush");
            }
        });
    }

    private void OnNumLockCellClicked(object sender, MouseButtonEventArgs e)
    {
        NumLockCellClicked?.Invoke();
    }
}
