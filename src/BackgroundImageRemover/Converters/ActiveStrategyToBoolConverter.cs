using System.Globalization;
using System.Windows.Data;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Converters;

/// <summary>
/// True when the Background Remover tool is active AND its selected strategy matches
/// ConverterParameter. Used to highlight the right per-strategy icon in the left toolbar
/// (values: [0] ActiveTool, [1] SelectedStrategy).
/// </summary>
public sealed class ActiveStrategyToBoolConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 2
           && values[0] is EditorTool.RemoveBackground
           && values[1] is StrategyKind kind
           && parameter is string name
           && kind.ToString() == name;

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => [Binding.DoNothing, Binding.DoNothing];
}
