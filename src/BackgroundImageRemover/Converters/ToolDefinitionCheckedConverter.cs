using System.Globalization;
using System.Windows.Data;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.ViewModels.Tools;

namespace BackgroundImageRemover.Converters;

/// <summary>True when the bound IToolDefinition is the currently active tool/strategy.
/// Read-only (see <see cref="StrategyToolbar"/> code-behind for how clicks actually select a
/// tool -- selection is a side effect, not something a converter can express safely).
/// Values: [0] the IToolDefinition item, [1] ActiveTool, [2] SelectedStrategy.</summary>
public sealed class ToolDefinitionCheckedConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 3
           && values[0] is IToolDefinition def
           && values[1] is EditorTool activeTool
           && values[2] is StrategyKind selectedStrategy
           && def.IsActive(activeTool, selectedStrategy);

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => [Binding.DoNothing, Binding.DoNothing, Binding.DoNothing];
}
