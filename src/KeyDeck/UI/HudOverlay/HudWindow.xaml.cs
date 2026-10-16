using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Image = System.Windows.Controls.Image;
using KeyDeck.Core;
using KeyDeck.Core.Models;
using KeyDeck.Platform.Win32;

namespace KeyDeck.UI.HudOverlay;

public partial class HudWindow : Window
{
    private sealed class MiniCellWidgets
    {
        public required Border Border { get; init; }
        public required TextBlock KeyLegend { get; init; }
        public required Image IconImage { get; init; }
        public required TextBlock ActionLabel { get; init; }
        public CancellationTokenSource? FlashCts { get; set; }
    }

    private static readonly SolidColorBrush DefaultCellBg = CreateFrozenBrush(0x2D, 0x2D, 0x30);
    private static readonly SolidColorBrush DefaultCellBorder = CreateFrozenBrush(0x3E, 0x3E, 0x44);
    private static readonly SolidColorBrush FlashCellBg = CreateFrozenBrush(0x00, 0x78, 0xD4);
    private static readonly SolidColorBrush FlashCellBorder = CreateFrozenBrush(0x1A, 0x8A, 0xD9);
    private static readonly SolidColorBrush AssignedLabelBrush = CreateFrozenBrush(0xE8, 0xE8, 0xE8);
    private static readonly SolidColorBrush UnassignedLabelBrush = CreateFrozenBrush(0x6E, 0x6E, 0x73);
    private static readonly SolidColorBrush LegendBrush = CreateFrozenBrush(0x8A, 0x8A, 0x90);
    private static readonly SolidColorBrush LockedBorderBrush = CreateFrozenBrush(0x55, 0x55, 0x5C);
    private static readonly SolidColorBrush UnlockedBorderBrush = CreateFrozenBrush(0x00, 0x78, 0xD4);

    private readonly Dictionary<NumpadKeyId, MiniCellWidgets> _miniCells = new();
    private HudVisibilityMode _visibilityMode = HudVisibilityMode.AlwaysOnWhenNumLockOff;
    private bool _isMacroActive;
    private bool _isLocked = true;
    private double _configuredOpacity = 0.85;
    private string _iconsDirectory = string.Empty;
    private CancellationTokenSource? _fadeCts;

    /// <summary>
    /// Raised when the user drags the unlocked HUD window to a new screen position.
    /// </summary>
    public event Action<double, double>? PositionChanged;

    /// <summary>
    /// Raised when the user clicks the ✕ close button on the unlocked HUD overlay.
    /// </summary>
    public event Action? HideRequested;

    public HudWindow()
    {
        InitializeComponent();
        BuildMiniCells();
        Loaded += OnLoaded;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetClickThrough(this, _isLocked);
        EnsureWithinScreenBounds();
    }

    private void BuildMiniCells()
    {
        var layout = new (NumpadKeyId id, int row, int col, int rowSpan, int colSpan, string legend)[]
        {
            (NumpadKeyId.NumpadDivide,   0, 1, 1, 1, "/"),
            (NumpadKeyId.NumpadMultiply, 0, 2, 1, 1, "*"),
            (NumpadKeyId.NumpadSubtract, 0, 3, 1, 1, "-"),

            (NumpadKeyId.Numpad7,        1, 0, 1, 1, "7"),
            (NumpadKeyId.Numpad8,        1, 1, 1, 1, "8"),
            (NumpadKeyId.Numpad9,        1, 2, 1, 1, "9"),
            (NumpadKeyId.NumpadAdd,      1, 3, 2, 1, "+"),

            (NumpadKeyId.Numpad4,        2, 0, 1, 1, "4"),
            (NumpadKeyId.Numpad5,        2, 1, 1, 1, "5"),
            (NumpadKeyId.Numpad6,        2, 2, 1, 1, "6"),

            (NumpadKeyId.Numpad1,        3, 0, 1, 1, "1"),
            (NumpadKeyId.Numpad2,        3, 1, 1, 1, "2"),
            (NumpadKeyId.Numpad3,        3, 2, 1, 1, "3"),
            (NumpadKeyId.NumpadEnter,    3, 3, 2, 1, "Ent"),

            (NumpadKeyId.Numpad0,        4, 0, 1, 2, "0"),
            (NumpadKeyId.NumpadDecimal, 4, 2, 1, 1, ".")
        };

        foreach (var item in layout)
        {
            var cellGrid = new Grid { Margin = new Thickness(2) };
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var legendText = new TextBlock
            {
                Text = item.legend,
                FontSize = 7.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = LegendBrush,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(1, 0, 0, 0)
            };
            Grid.SetRow(legendText, 0);

            var iconImage = new Image
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);
            Grid.SetRow(iconImage, 1);

            var labelText = new TextBlock
            {
                Text = item.legend,
                FontSize = 8.5,
                Foreground = UnassignedLabelBrush,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(1, 0, 1, 1)
            };
            Grid.SetRow(labelText, 2);

            cellGrid.Children.Add(legendText);
            cellGrid.Children.Add(iconImage);
            cellGrid.Children.Add(labelText);

            var border = new Border
            {
                Background = DefaultCellBg,
                BorderBrush = DefaultCellBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Child = cellGrid,
                SnapsToDevicePixels = true
            };

            Grid.SetRow(border, item.row);
            Grid.SetColumn(border, item.col);
            if (item.rowSpan > 1) Grid.SetRowSpan(border, item.rowSpan);
            if (item.colSpan > 1) Grid.SetColumnSpan(border, item.colSpan);

            GridContainer.Children.Add(border);

            _miniCells[item.id] = new MiniCellWidgets
            {
                Border = border,
                KeyLegend = legendText,
                IconImage = iconImage,
                ActionLabel = labelText
            };
        }
    }

    public void ConfigureFromSettings(AppSettings settings, string iconsDirectory)
    {
        _iconsDirectory = iconsDirectory;
        _configuredOpacity = Math.Clamp(settings.HudOpacity, 0.20, 1.0);
        _visibilityMode = settings.HudMode;

        if (settings.HudLeft.HasValue && settings.HudTop.HasValue)
        {
            Left = settings.HudLeft.Value;
            Top = settings.HudTop.Value;
            EnsureWithinScreenBounds();
        }
        else
        {
            ResetPositionToBottomRight();
        }

        SetLockedState(settings.IsHudLocked);
        ApplyVisibilityRules();
    }

    public void ResetPositionToBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 24;
        PositionChanged?.Invoke(Left, Top);
    }

    private void EnsureWithinScreenBounds()
    {
        var vLeft   = SystemParameters.VirtualScreenLeft;
        var vTop    = SystemParameters.VirtualScreenTop;
        var vRight  = vLeft + SystemParameters.VirtualScreenWidth;
        var vBottom = vTop + SystemParameters.VirtualScreenHeight;

        if (Left < vLeft || Left + Width > vRight || Top < vTop || Top + Height > vBottom)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;
        }
    }

    public void SetLockedState(bool isLocked)
    {
        _isLocked = isLocked;
        Dispatcher.InvokeAsync(() =>
        {
            WindowHelper.SetClickThrough(this, _isLocked);
            HudContainer.BorderBrush = _isLocked ? LockedBorderBrush : UnlockedBorderBrush;
            HudContainer.BorderThickness = _isLocked ? new Thickness(1) : new Thickness(2);
            DragHintBadge.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
            HudCloseButton.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
            Cursor = _isLocked ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.SizeAll;

            // When unlocked for positioning, show the HUD even if FadeOutOnPress is selected
            ApplyVisibilityRules();
        });
    }

    public void SetHudOpacity(double opacity)
    {
        _configuredOpacity = Math.Clamp(opacity, 0.20, 1.0);
        Dispatcher.InvokeAsync(() =>
        {
            // Clear any active HoldEnd animation clock before assigning local Opacity
            BeginAnimation(OpacityProperty, null);
            Opacity = _configuredOpacity;
        });
    }

    public void SetVisibilityMode(HudVisibilityMode mode)
    {
        _visibilityMode = mode;
        ApplyVisibilityRules();
    }

    public void UpdateMacroState(bool isMacroActive)
    {
        _isMacroActive = isMacroActive;
        Dispatcher.InvokeAsync(() =>
        {
            MiniNumLockDot.Fill = isMacroActive ? FlashCellBg : LegendBrush;
        });
        ApplyVisibilityRules();
    }

    private void ApplyVisibilityRules()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _fadeCts?.Cancel();
            // Always clear any prior fade-out animation clock so Opacity assignment takes effect
            BeginAnimation(OpacityProperty, null);

            if (_visibilityMode == HudVisibilityMode.Disabled)
            {
                Hide();
                return;
            }

            // If user unlocked the HUD to drag it around, keep it visible while they position it
            if (!_isLocked)
            {
                Opacity = _configuredOpacity;
                Show();
                return;
            }

            if (_visibilityMode == HudVisibilityMode.AlwaysOnWhenNumLockOff)
            {
                if (_isMacroActive)
                {
                    Opacity = _configuredOpacity;
                    Show();
                }
                else
                {
                    Hide();
                }
            }
            else if (_visibilityMode == HudVisibilityMode.FadeOutOnPress)
            {
                if (_isMacroActive)
                {
                    TriggerTemporaryVisibility();
                }
                else
                {
                    Hide();
                }
            }
        });
    }

    public void TriggerTemporaryVisibility()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_visibilityMode == HudVisibilityMode.Disabled || (!_isMacroActive && _isLocked))
            {
                return;
            }

            _fadeCts?.Cancel();
            _fadeCts = new CancellationTokenSource();
            var token = _fadeCts.Token;

            // Detach any existing animation clock before restoring Opacity
            BeginAnimation(OpacityProperty, null);
            Opacity = _configuredOpacity;
            Show();

            if (!_isLocked)
            {
                return;
            }

            Task.Delay(2500, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        var anim = new DoubleAnimation(_configuredOpacity, 0.0, TimeSpan.FromMilliseconds(400))
                        {
                            FillBehavior = FillBehavior.Stop
                        };
                        anim.Completed += (s, e) =>
                        {
                            BeginAnimation(OpacityProperty, null);
                            Opacity = 0.0;
                            Hide();
                        };
                        BeginAnimation(OpacityProperty, anim);
                    });
                }
            }, TaskScheduler.Default);
        });
    }

    public void FlashKey(NumpadKeyId keyId)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (_visibilityMode == HudVisibilityMode.FadeOutOnPress)
            {
                TriggerTemporaryVisibility();
            }

            if (_miniCells.TryGetValue(keyId, out var cell))
            {
                cell.FlashCts?.Cancel();
                var cts = new CancellationTokenSource();
                cell.FlashCts = cts;

                cell.Border.Background = FlashCellBg;
                cell.Border.BorderBrush = FlashCellBorder;

                try
                {
                    await Task.Delay(200, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (cell.FlashCts == cts)
                {
                    cell.Border.Background = DefaultCellBg;
                    cell.Border.BorderBrush = DefaultCellBorder;
                }
            }
        });
    }

    public void UpdateProfile(Profile profile, string? iconsDirectory = null)
    {
        if (!string.IsNullOrEmpty(iconsDirectory))
        {
            _iconsDirectory = iconsDirectory;
        }

        Dispatcher.InvokeAsync(() =>
        {
            ProfileNameText.Text = profile.Name;

            foreach (var kvp in _miniCells)
            {
                var keyId = kvp.Key;
                var cell = kvp.Value;
                var action = profile.GetAction(keyId);

                // Resolve icon
                ImageSource? icon = null;
                if (!string.IsNullOrWhiteSpace(action.IconPath))
                {
                    icon = IconExtractor.LoadFrozenBitmap(action.IconPath);
                }
                else if (action.Type == ActionType.LaunchApp && !string.IsNullOrWhiteSpace(action.TargetPath))
                {
                    icon = IconExtractor.ExtractOrGetCachedIcon(action.TargetPath, _iconsDirectory);
                }

                if (icon != null)
                {
                    cell.IconImage.Source = icon;
                    cell.IconImage.Visibility = Visibility.Visible;
                }
                else
                {
                    cell.IconImage.Source = null;
                    cell.IconImage.Visibility = Visibility.Collapsed;
                }

                // Resolve label
                if (action.Type != ActionType.None && !string.IsNullOrWhiteSpace(action.Label))
                {
                    cell.ActionLabel.Text = action.Label;
                    cell.ActionLabel.Foreground = AssignedLabelBrush;
                }
                else
                {
                    cell.ActionLabel.Text = "—";
                    cell.ActionLabel.Foreground = UnassignedLabelBrush;
                }
            }
        });
    }

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
                PositionChanged?.Invoke(Left, Top);
            }
            catch
            {
                // Ignore if mouse button was released before DragMove started
            }
        }
    }

    private void OnHudCloseButtonClick(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke();
    }
}
