using System.Globalization;
using System.Windows.Data;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Converters;

/// <summary>
/// Converts ActionType enum values to user-friendly UI display strings.
/// </summary>
public class ActionTypeToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ActionType actionType)
        {
            return actionType switch
            {
                ActionType.None => "(None)",
                ActionType.LaunchApp => "Launch Application",
                ActionType.OpenUrl => "Open Webpage URL",
                ActionType.OpenFolder => "Open Folder",
                ActionType.Hotkey => "Keyboard Hotkey",
                ActionType.RunScript => "Run Script (.ps1 / .bat)",
                ActionType.MediaControl => "Media Playback Control",
                ActionType.SystemAction => "Windows System Action",
                ActionType.TextPaste => "Paste Text Snippet",
                ActionType.MultiAction => "Multi-Action Sequence",
                _ => actionType.ToString()
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
