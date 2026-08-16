using System.Globalization;
using System.Windows.Data;

namespace BackgroundImageRemover.Converters;

/// <summary>True when the bound enum value's name equals ConverterParameter. Supports two-way
/// binding (e.g. RadioButton.IsChecked) by parsing ConverterParameter back into the enum type.</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is string name && value.ToString() == name;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}
