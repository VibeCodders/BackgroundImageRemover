using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Converters;

/// <summary>Visible when the bound StrategyKind equals the ConverterParameter (a StrategyKind name).</summary>
public sealed class StrategyVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StrategyKind kind && parameter is string expectedName
            && Enum.TryParse<StrategyKind>(expectedName, out var expected))
        {
            return kind == expected ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when the bound StrategyKind is GrabCut (used to enable rectangle drawing).</summary>
public sealed class StrategyEqualsGrabCutConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is StrategyKind.GrabCut;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
