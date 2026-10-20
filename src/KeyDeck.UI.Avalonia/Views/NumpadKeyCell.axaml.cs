using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Avalonia.Views;

public partial class NumpadKeyCell : UserControl
{
    private static readonly IBrush DefaultBg = SolidColorBrush.Parse("#1E1E22");
    private static readonly IBrush DefaultBorder = SolidColorBrush.Parse("#3F3F46");
    private static readonly IBrush SelectedBorder = SolidColorBrush.Parse("#0078D6");
    private static readonly IBrush FlashBg = SolidColorBrush.Parse("#0078D6");
    private static readonly IBrush FlashBorder = SolidColorBrush.Parse("#1A88E1");

    private bool _isSelected;
    private CancellationTokenSource? _flashCts;

    public NumpadKeyId KeyId { get; set; }

    public string Legend
    {
        get => LegendText.Text ?? string.Empty;
        set => LegendText.Text = value;
    }

    public string ActionLabel
    {
        get => ActionLabelText.Text ?? string.Empty;
        set => ActionLabelText.Text = string.IsNullOrWhiteSpace(value) ? "(Unassigned)" : value;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            UpdateSelectionVisual();
        }
    }

    public event Action<NumpadKeyCell>? CellClicked;

    public NumpadKeyCell()
    {
        InitializeComponent();
        PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CellClicked?.Invoke(this);
    }

    public void UpdateFromAction(KeyAction action)
    {
        ActionLabel = !string.IsNullOrWhiteSpace(action.Label) ? action.Label : GetDefaultLabel(action);

        ActionBadgeText.Text = action.Type switch
        {
            ActionType.LaunchApp     => "APP",
            ActionType.OpenUrl       => "WEB",
            ActionType.OpenFolder    => "FOLDER",
            ActionType.Hotkey        => "HOTKEY",
            ActionType.RunScript     => "SCRIPT",
            ActionType.MediaControl  => "MEDIA",
            ActionType.SystemAction  => "SYS",
            ActionType.TextPaste     => "TEXT",
            ActionType.SwitchProfile => "PROFILE",
            ActionType.MultiAction   => "MACRO",
            _                        => "NONE"
        };

        BadgeBorder.Background = action.Type switch
        {
            ActionType.None => SolidColorBrush.Parse("#27272A"),
            ActionType.LaunchApp => SolidColorBrush.Parse("#1E3A5F"),
            ActionType.OpenUrl => SolidColorBrush.Parse("#1E3A8A"),
            ActionType.Hotkey => SolidColorBrush.Parse("#3B1D54"),
            ActionType.MediaControl => SolidColorBrush.Parse("#14532D"),
            ActionType.SystemAction => SolidColorBrush.Parse("#7F1D1D"),
            ActionType.TextPaste => SolidColorBrush.Parse("#064E3B"),
            _ => SolidColorBrush.Parse("#27272A")
        };

        ActionBadgeText.Foreground = action.Type == ActionType.None
            ? SolidColorBrush.Parse("#71717A")
            : SolidColorBrush.Parse("#93C5FD");
    }

    private static string GetDefaultLabel(KeyAction action) => action.Type switch
    {
        ActionType.LaunchApp => "Launch App",
        ActionType.OpenUrl => "Open Web URL",
        ActionType.OpenFolder => "Open Folder",
        ActionType.Hotkey => action.HotkeyDisplay ?? "Hotkey",
        ActionType.MediaControl => action.MediaCommand.ToString(),
        ActionType.SystemAction => action.SystemCommand.ToString(),
        ActionType.TextPaste => "Text Snippet",
        ActionType.SwitchProfile => "Switch Profile",
        ActionType.MultiAction => "Multi-Action",
        _ => "(Unassigned)"
    };

    public void FlashKeyPress()
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            _flashCts?.Cancel();
            var cts = new CancellationTokenSource();
            _flashCts = cts;

            CellBorder.Background = FlashBg;
            CellBorder.BorderBrush = FlashBorder;

            try
            {
                await Task.Delay(200, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (_flashCts == cts)
            {
                CellBorder.Background = DefaultBg;
                UpdateSelectionVisual();
            }
        });
    }

    private void UpdateSelectionVisual()
    {
        if (_isSelected)
        {
            CellBorder.BorderBrush = SelectedBorder;
            CellBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            CellBorder.BorderBrush = DefaultBorder;
            CellBorder.BorderThickness = new Thickness(1);
        }
    }
}
