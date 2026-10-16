using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyDeck.Core.Models;
using KeyDeck.Platform.Win32;

namespace KeyDeck.UI.HudOverlay;

public partial class HudWindow : Window
{
    private readonly Dictionary<NumpadKeyId, (Border border, TextBlock textBlock)> _miniCells = new();
    private HudVisibilityMode _visibilityMode = HudVisibilityMode.AlwaysOnWhenNumLockOff;
    private bool _isMacroActive;
    private CancellationTokenSource? _fadeCts;

    public HudWindow()
    {
        InitializeComponent();
        RegisterMiniCells();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.MakeClickThrough(this);
        PositionInBottomRight();
    }

    private void RegisterMiniCells()
    {
        _miniCells[NumpadKeyId.Numpad0] = (Mini0, Text0);
        _miniCells[NumpadKeyId.Numpad1] = (Mini1, Text1);
        _miniCells[NumpadKeyId.Numpad2] = (Mini2, Text2);
        _miniCells[NumpadKeyId.Numpad3] = (Mini3, Text3);
        _miniCells[NumpadKeyId.Numpad4] = (Mini4, Text4);
        _miniCells[NumpadKeyId.Numpad5] = (Mini5, Text5);
        _miniCells[NumpadKeyId.Numpad6] = (Mini6, Text6);
        _miniCells[NumpadKeyId.Numpad7] = (Mini7, Text7);
        _miniCells[NumpadKeyId.Numpad8] = (Mini8, Text8);
        _miniCells[NumpadKeyId.Numpad9] = (Mini9, Text9);
        _miniCells[NumpadKeyId.NumpadDecimal] = (MiniDecimal, TextDecimal);
        _miniCells[NumpadKeyId.NumpadAdd] = (MiniAdd, TextAdd);
        _miniCells[NumpadKeyId.NumpadSubtract] = (MiniSubtract, TextSubtract);
        _miniCells[NumpadKeyId.NumpadMultiply] = (MiniMultiply, TextMultiply);
        _miniCells[NumpadKeyId.NumpadDivide] = (MiniDivide, TextDivide);
        _miniCells[NumpadKeyId.NumpadEnter] = (MiniEnter, TextEnter);
    }

    private void PositionInBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 24;
    }

    public void SetVisibilityMode(HudVisibilityMode mode)
    {
        _visibilityMode = mode;
        ApplyVisibilityRules();
    }

    public void UpdateMacroState(bool isMacroActive)
    {
        _isMacroActive = isMacroActive;
        ApplyVisibilityRules();
    }

    private void ApplyVisibilityRules()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_visibilityMode == HudVisibilityMode.Disabled)
            {
                Hide();
                return;
            }

            if (_visibilityMode == HudVisibilityMode.AlwaysOnWhenNumLockOff)
            {
                if (_isMacroActive)
                {
                    Opacity = 0.9;
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
            if (_visibilityMode == HudVisibilityMode.Disabled || !_isMacroActive)
            {
                return;
            }

            _fadeCts?.Cancel();
            _fadeCts = new CancellationTokenSource();
            var token = _fadeCts.Token;

            Opacity = 0.92;
            Show();

            Task.Delay(2500, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        var anim = new DoubleAnimation(0.92, 0.0, TimeSpan.FromMilliseconds(400));
                        anim.Completed += (s, e) =>
                        {
                            if (Opacity <= 0.05)
                            {
                                Hide();
                            }
                        };
                        BeginAnimation(OpacityProperty, anim);
                    });
                }
            }, TaskScheduler.Default);
        });
    }

    public void FlashKey(NumpadKeyId keyId)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_visibilityMode == HudVisibilityMode.FadeOutOnPress)
            {
                TriggerTemporaryVisibility();
            }

            if (_miniCells.TryGetValue(keyId, out var cell))
            {
                var originalBg = cell.border.Background;
                cell.border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));

                Task.Delay(250).ContinueWith(_ =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        cell.border.Background = originalBg;
                    });
                });
            }
        });
    }

    public void UpdateProfile(Profile profile)
    {
        Dispatcher.InvokeAsync(() =>
        {
            ProfileNameText.Text = profile.Name;

            foreach (var kvp in _miniCells)
            {
                var keyId = kvp.Key;
                var (border, textBlock) = kvp.Value;
                var action = profile.GetAction(keyId);

                if (!string.IsNullOrWhiteSpace(action.Label))
                {
                    textBlock.Text = action.Label;
                    textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
                }
                else
                {
                    textBlock.Text = GetDefaultLegend(keyId);
                    textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x77, 0x77, 0x77));
                }
            }
        });
    }

    private static string GetDefaultLegend(NumpadKeyId keyId)
    {
        return keyId switch
        {
            NumpadKeyId.NumpadDivide => "/",
            NumpadKeyId.NumpadMultiply => "*",
            NumpadKeyId.NumpadSubtract => "-",
            NumpadKeyId.NumpadAdd => "+",
            NumpadKeyId.NumpadDecimal => ".",
            NumpadKeyId.NumpadEnter => "Ent",
            _ => ((int)keyId).ToString()
        };
    }
}
