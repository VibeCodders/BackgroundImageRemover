using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// Shared labelled slider row: a bold caption on the left, the current value (optionally
/// formatted via <see cref="ValueFormat"/>, e.g. "{0:0}px") on the right, and the slider
/// below. Replaces the label + value + Slider boilerplate duplicated across every tool view.
/// </summary>
public partial class SliderField : UserControl
{
    public SliderField()
    {
        InitializeComponent();
    }

    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SliderField), new PropertyMetadata(null));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderField),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderField), new PropertyMetadata(0.0));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderField), new PropertyMetadata(100.0));

    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(SliderField), new PropertyMetadata(0.1));

    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(SliderField), new PropertyMetadata(1.0));

    public double TickFrequency
    {
        get => (double)GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public static readonly DependencyProperty TickFrequencyProperty =
        DependencyProperty.Register(nameof(TickFrequency), typeof(double), typeof(SliderField), new PropertyMetadata(1.0));

    public bool IsSnapToTickEnabled
    {
        get => (bool)GetValue(IsSnapToTickEnabledProperty);
        set => SetValue(IsSnapToTickEnabledProperty, value);
    }

    public static readonly DependencyProperty IsSnapToTickEnabledProperty =
        DependencyProperty.Register(nameof(IsSnapToTickEnabled), typeof(bool), typeof(SliderField), new PropertyMetadata(false));

    /// <summary>Optional .NET format string for the value text (e.g. "{0:0}px", "{0:P0}").</summary>
    public string? ValueFormat
    {
        get => (string?)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public static readonly DependencyProperty ValueFormatProperty =
        DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(SliderField),
            new PropertyMetadata(null, OnValueFormatChanged));

    /// <summary>Formatted display text, recomputed from <see cref="Value"/> and <see cref="ValueFormat"/>.</summary>
    public string? ValueText
    {
        get => (string?)GetValue(ValueTextProperty);
        private set => SetValue(ValueTextProperty, value);
    }

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(SliderField), new PropertyMetadata(null));

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SliderField)d).UpdateValueText();

    private static void OnValueFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SliderField)d).UpdateValueText();

    /// <summary>
    /// Formats the value with the element's <see cref="FrameworkElement.Language"/> culture — the
    /// same culture WPF bindings use by default — so the text matches the previous per-view bindings.
    /// </summary>
    private void UpdateValueText()
    {
        CultureInfo culture = Language.GetSpecificCulture();
        if (string.IsNullOrEmpty(ValueFormat))
        {
            ValueText = Value.ToString(culture);
        }
        else
        {
            try
            {
                ValueText = string.Format(culture, ValueFormat, Value);
            }
            catch (FormatException)
            {
                ValueText = Value.ToString(culture);
            }
        }
    }
}
