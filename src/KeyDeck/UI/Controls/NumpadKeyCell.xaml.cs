using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Controls;

public partial class NumpadKeyCell : UserControl
{
    private bool _isSelected;

    public NumpadKeyId KeyId { get; set; }

    public string Legend
    {
        get => LegendText.Text;
        set => LegendText.Text = value;
    }

    public string ActionLabel
    {
        get => ActionLabelText.Text;
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
    public event Action<NumpadKeyCell>? CellDoubleClicked;
    public event Action<NumpadKeyId, string>? FileDropped;

    public NumpadKeyCell()
    {
        InitializeComponent();
    }

    public void SetIcon(ImageSource? icon)
    {
        if (icon != null)
        {
            ActionIcon.Source = icon;
            ActionIcon.Visibility = Visibility.Visible;
        }
        else
        {
            ActionIcon.Source = null;
            ActionIcon.Visibility = Visibility.Collapsed;
        }
    }

    public void FlashKeyPress()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            var originalBg = CellBorder.Background;
            var originalBorder = CellBorder.BorderBrush;

            CellBorder.Background = (Brush)FindResource("AccentBrush");
            CellBorder.BorderBrush = (Brush)FindResource("AccentHoverBrush");

            await Task.Delay(250);

            CellBorder.Background = originalBg;
            CellBorder.BorderBrush = originalBorder;
        });
    }

    private void UpdateSelectionVisual()
    {
        if (_isSelected)
        {
            CellBorder.BorderBrush = (Brush)FindResource("AccentBrush");
            CellBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            CellBorder.BorderBrush = (Brush)FindResource("BorderBrush");
            CellBorder.BorderThickness = new Thickness(1);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            CellDoubleClicked?.Invoke(this);
        }
        else
        {
            CellClicked?.Invoke(this);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var filePath = files[0];
                FileDropped?.Invoke(KeyId, filePath);
                e.Handled = true;
            }
        }
    }
}
