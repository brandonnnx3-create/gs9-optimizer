using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ConnectionOptimizer.ViewModels;

namespace ConnectionOptimizer.Views;

/// <summary>
/// Glyph + word for a state. Each state differs in shape and wording; color is a third, optional signal.
/// </summary>
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(StatusKind), typeof(StatusBadge),
        new PropertyMetadata(StatusKind.Ready, (d, _) => ((StatusBadge)d).UpdateVisual()));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StatusBadge),
        new PropertyMetadata(string.Empty, (d, e) => ((StatusBadge)d).LabelText.Text = e.NewValue as string ?? string.Empty));

    private static readonly DoubleAnimation Pulse = CreatePulse();

    public StatusBadge()
    {
        InitializeComponent();
        UpdateGlyphSize();
        UpdateVisual();
    }

    public StatusKind Kind
    {
        get => (StatusKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty)
        {
            UpdateGlyphSize();
        }
    }

    private void UpdateGlyphSize()
    {
        if (GlyphText is not null)
        {
            GlyphText.FontSize = FontSize * 0.72;
        }
    }

    private void UpdateVisual()
    {
        (string glyph, string glyphBrush, string textBrush, bool pulse) = Kind switch
        {
            StatusKind.Running or StatusKind.Optimizing => ("●", "Brush.Text", "Brush.Text", true),
            StatusKind.Waiting => ("◆", "Brush.Text", "Brush.Text", true),
            StatusKind.Checking => ("●", "Brush.TextSecondary", "Brush.TextSecondary", true),
            StatusKind.Applied => ("✓", "Brush.Ok", "Brush.Text", false),
            StatusKind.Completed or StatusKind.Active => ("●", "Brush.Ok", "Brush.Text", false),
            StatusKind.Partial => ("◐", "Brush.Text", "Brush.Text", false),
            StatusKind.Inactive => ("○", "Brush.TextSecondary", "Brush.TextSecondary", false),
            StatusKind.Error => ("✕", "Brush.Error", "Brush.Error", false),
            _ => ("●", "Brush.Text", "Brush.Text", false),
        };

        GlyphText.Text = glyph;
        GlyphText.Foreground = Resolve(glyphBrush);
        LabelText.Foreground = Resolve(textBrush);

        if (pulse)
        {
            GlyphText.BeginAnimation(OpacityProperty, Pulse);
        }
        else
        {
            GlyphText.BeginAnimation(OpacityProperty, null);
            GlyphText.Opacity = 1;
        }
    }

    private Brush Resolve(string key) => TryFindResource(key) as Brush ?? Brushes.White;

    private static DoubleAnimation CreatePulse()
    {
        var animation = new DoubleAnimation(1.0, 0.2, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        animation.Freeze();
        return animation;
    }
}
