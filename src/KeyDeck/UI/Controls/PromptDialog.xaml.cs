using System.Windows;
using System.Windows.Input;

namespace KeyDeck.UI.Controls;

public partial class PromptDialog : Window
{
    public string? ResponseText { get; private set; }

    public PromptDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        InputTextBox.Text = defaultValue;
        InputTextBox.SelectAll();
        Loaded += (s, e) => InputTextBox.Focus();
    }

    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        var dialog = new PromptDialog(title, prompt, defaultValue);
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        var result = dialog.ShowDialog();
        return result == true ? dialog.ResponseText : null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ResponseText = InputTextBox.Text;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnOkClick(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            OnCancelClick(sender, e);
        }
    }
}
