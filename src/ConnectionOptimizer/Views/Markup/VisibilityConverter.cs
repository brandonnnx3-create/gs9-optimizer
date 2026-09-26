using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ConnectionOptimizer.Views.Markup;

/// <summary>
/// true, non-empty text or any non-null object → Visible; otherwise Collapsed.
/// ConverterParameter=invert flips the result.
/// </summary>
public sealed class VisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visible = value switch
        {
            bool flag => flag,
            string text => text.Length > 0,
            null => false,
            _ => true,
        };

        if (parameter is string mode && mode.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
