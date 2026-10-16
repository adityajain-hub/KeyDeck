using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KeyDeck.UI.Converters;

/// <summary>
/// Converts boolean to System.Windows.Visibility (with optional invert parameter).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var invert = parameter is string paramStr && paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            boolValue = !boolValue;
        }

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isVisible = value is Visibility visibility && visibility == Visibility.Visible;
        var invert = parameter is string paramStr && paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !isVisible : isVisible;
    }
}
