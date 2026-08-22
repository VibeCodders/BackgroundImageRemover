using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BackgroundImageRemover.Converters;

/// <summary>Resolves an <see cref="Models.EditorTool"/>/strategy icon's resource key (e.g.
/// "RetouchIcon", defined in Themes/StrategyIcons.xaml) into the actual Geometry, so the tool
/// palette can be built data-driven from IToolDefinition.IconResourceKey.</summary>
public sealed class ResourceKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key ? Application.Current.TryFindResource(key) as Geometry : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
