using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Markup;

namespace ConnectionOptimizer.Views.Markup;

/// <summary>
/// WPF has no letter-spacing. Wide tracking on uppercase labels is part of the G.S.9 look,
/// so a thin space (U+2009) is inserted between characters.
/// </summary>
public static class Tracking
{
    private const char ThinSpace = ' ';

    public static string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            builder.Append(text[i]);
            if (i < text.Length - 1)
            {
                builder.Append(ThinSpace);
            }
        }

        return builder.ToString();
    }
}

/// <summary>Usage: Text="{m:Tracked 'SYSTEM STATUS'}".</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrackedExtension : MarkupExtension
{
    public TrackedExtension()
    {
    }

    public TrackedExtension(string text) => Text = text;

    [ConstructorArgument("text")]
    public string Text { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Tracking.Apply(Text);
}

/// <summary>Tracking for bound text.</summary>
public sealed class TrackingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Tracking.Apply(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
