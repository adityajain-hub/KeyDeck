using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Avalonia.Views;

public partial class HudWindow : Window
{
    private readonly IAppStateService _appState = null!;
    private readonly IProfileManager _profileManager = null!;
    private readonly Dictionary<NumpadKeyId, TextBlock> _tileLabels = new();
    private bool _isDragging;
    private Point _dragStart;

    public HudWindow() { InitializeComponent(); }

    public HudWindow(IAppStateService appState, IProfileManager profileManager)
    {
        InitializeComponent();

        _appState = appState;
        _profileManager = profileManager;

        if (_appState == null) return;

        Opacity = _appState.HudOpacity;

        _appState.StateChanged += OnAppStateChanged;
        _appState.ResetHudPositionRequested += OnResetPosition;
        _profileManager.ActiveProfileChanged += OnActiveProfileChanged;
        _profileManager.BindingChanged += OnBindingChanged;

        BuildCompactGrid();
        RefreshDisplay();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    private void BuildCompactGrid()
    {
        HudGrid.Children.Clear();
        _tileLabels.Clear();

        // Row 0
        AddTile("NL", 0, 0, null);
        AddTile("/", 0, 1, NumpadKeyId.NumpadDivide);
        AddTile("*", 0, 2, NumpadKeyId.NumpadMultiply);
        AddTile("-", 0, 3, NumpadKeyId.NumpadSubtract);

        // Row 1
        AddTile("7", 1, 0, NumpadKeyId.Numpad7);
        AddTile("8", 1, 1, NumpadKeyId.Numpad8);
        AddTile("9", 1, 2, NumpadKeyId.Numpad9);
        AddTile("+", 1, 3, NumpadKeyId.NumpadAdd, rowSpan: 2);

        // Row 2
        AddTile("4", 2, 0, NumpadKeyId.Numpad4);
        AddTile("5", 2, 1, NumpadKeyId.Numpad5);
        AddTile("6", 2, 2, NumpadKeyId.Numpad6);

        // Row 3
        AddTile("1", 3, 0, NumpadKeyId.Numpad1);
        AddTile("2", 3, 1, NumpadKeyId.Numpad2);
        AddTile("3", 3, 2, NumpadKeyId.Numpad3);
        AddTile("↵", 3, 3, NumpadKeyId.NumpadEnter, rowSpan: 2);

        // Row 4
        AddTile("0", 4, 0, NumpadKeyId.Numpad0, colSpan: 2);
        AddTile(".", 4, 2, NumpadKeyId.NumpadDecimal);
    }

    private void AddTile(string legend, int row, int col, NumpadKeyId? keyId, int rowSpan = 1, int colSpan = 1)
    {
        var border = new Border
        {
            Background = SolidColorBrush.Parse("#27272A"),
            BorderBrush = SolidColorBrush.Parse("#3F3F46"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(2)
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        if (rowSpan > 1) Grid.SetRowSpan(border, rowSpan);
        if (colSpan > 1) Grid.SetColumnSpan(border, colSpan);

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 2
        };

        var legendBlock = new TextBlock
        {
            Text = legend,
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#A1A1AA"),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var labelBlock = new TextBlock
        {
            Text = "-",
            FontSize = 9,
            Foreground = SolidColorBrush.Parse("#F4F4F5"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = colSpan > 1 ? 90 : 50,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        stack.Children.Add(legendBlock);
        stack.Children.Add(labelBlock);
        border.Child = stack;

        if (keyId.HasValue)
        {
            _tileLabels[keyId.Value] = labelBlock;
        }

        HudGrid.Children.Add(border);
    }

    private void RefreshDisplay()
    {
        if (_profileManager == null) return;

        var profile = _profileManager.ActiveProfile;
        ProfileNameText.Text = $" ({profile.Name})";

        foreach (var kvp in _tileLabels)
        {
            var action = profile.GetAction(kvp.Key);
            kvp.Value.Text = !string.IsNullOrWhiteSpace(action.Label)
                ? action.Label
                : (action.Type == ActionType.None ? "-" : action.Type.ToString());
        }
    }

    private void OnAppStateChanged(AppStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.Property == AppStateProperty.HudOpacity && e.NewValue is double opacity)
            {
                Opacity = opacity;
            }
            else if (e.Property == AppStateProperty.HudVisibilityMode && e.NewValue is HudVisibilityMode mode)
            {
                IsVisible = (mode != HudVisibilityMode.Disabled);
            }
        });
    }

    private void OnActiveProfileChanged(Profile profile)
    {
        Dispatcher.UIThread.Post(RefreshDisplay);
    }

    private void OnBindingChanged(NumpadKeyId keyId, KeyAction action)
    {
        Dispatcher.UIThread.Post(RefreshDisplay);
    }

    private void OnResetPosition()
    {
        Position = new PixelPoint(100, 100);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
        _appState?.SetHudMode(HudVisibilityMode.Disabled);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_appState?.IsHudLocked == true) return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            var current = e.GetPosition(this);
            var deltaX = (int)(current.X - _dragStart.X);
            var deltaY = (int)(current.Y - _dragStart.Y);
            Position = new PixelPoint(Position.X + deltaX, Position.Y + deltaY);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }
}
