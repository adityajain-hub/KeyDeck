using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Avalonia.Views;

public partial class NumpadGridControl : UserControl
{
    private readonly Dictionary<NumpadKeyId, NumpadKeyCell> _cells = new();
    private NumpadKeyId _selectedKey = NumpadKeyId.Numpad7;

    public NumpadKeyId SelectedKey => _selectedKey;

    public event Action<NumpadKeyId>? KeySelected;

    public NumpadGridControl()
    {
        InitializeComponent();
        RegisterCells();
    }

    private void RegisterCells()
    {
        MapCell(NumpadKeyId.Numpad0, Cell0);
        MapCell(NumpadKeyId.Numpad1, Cell1);
        MapCell(NumpadKeyId.Numpad2, Cell2);
        MapCell(NumpadKeyId.Numpad3, Cell3);
        MapCell(NumpadKeyId.Numpad4, Cell4);
        MapCell(NumpadKeyId.Numpad5, Cell5);
        MapCell(NumpadKeyId.Numpad6, Cell6);
        MapCell(NumpadKeyId.Numpad7, Cell7);
        MapCell(NumpadKeyId.Numpad8, Cell8);
        MapCell(NumpadKeyId.Numpad9, Cell9);
        MapCell(NumpadKeyId.NumpadDecimal, CellDecimal);
        MapCell(NumpadKeyId.NumpadAdd, CellAdd);
        MapCell(NumpadKeyId.NumpadSubtract, CellSubtract);
        MapCell(NumpadKeyId.NumpadMultiply, CellMultiply);
        MapCell(NumpadKeyId.NumpadDivide, CellDivide);
        MapCell(NumpadKeyId.NumpadEnter, CellEnter);
    }

    private void MapCell(NumpadKeyId id, NumpadKeyCell cell)
    {
        cell.KeyId = id;
        cell.CellClicked += OnCellClicked;
        _cells[id] = cell;
    }

    private void OnCellClicked(NumpadKeyCell cell)
    {
        SelectKey(cell.KeyId);
        KeySelected?.Invoke(cell.KeyId);
    }

    public void SelectKey(NumpadKeyId keyId)
    {
        _selectedKey = keyId;

        foreach (var kvp in _cells)
        {
            kvp.Value.IsSelected = (kvp.Key == keyId);
        }
    }

    public void RefreshProfile(Profile profile)
    {
        foreach (var kvp in _cells)
        {
            var action = profile.GetAction(kvp.Key);
            kvp.Value.UpdateFromAction(action);
        }
    }

    public void FlashKey(NumpadKeyId keyId)
    {
        if (_cells.TryGetValue(keyId, out var cell))
        {
            cell.FlashKeyPress();
        }
    }

    public void UpdateNumLockStatus(bool isNumLockOn, bool isMacroModeActive)
    {
        if (isMacroModeActive)
        {
            NumLockIndicatorDot.Fill = SolidColorBrush.Parse("#22C55E");
            NumLockStatusLabel.Text = "MACROS ACTIVE";
            NumLockStatusLabel.Foreground = SolidColorBrush.Parse("#22C55E");
        }
        else
        {
            NumLockIndicatorDot.Fill = SolidColorBrush.Parse("#71717A");
            NumLockStatusLabel.Text = isNumLockOn ? "NUMPAD PASS-THROUGH" : "DISABLED";
            NumLockStatusLabel.Foreground = SolidColorBrush.Parse("#71717A");
        }
    }
}
