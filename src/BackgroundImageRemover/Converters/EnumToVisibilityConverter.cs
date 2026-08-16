using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BackgroundImageRemover.Converters;

/// <summary>
/// Visible when the bound enum value's name matches ConverterParameter (a "|"-separated list
/// of accepted names, e.g. "Brush|MagicWand"). Works with any enum type.
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string names)
        {
            return Visibility.Collapsed;
        }
        var accepted = names.Split('|', StringSplitOptions.TrimEntries);
        return accepted.Contains(value.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
