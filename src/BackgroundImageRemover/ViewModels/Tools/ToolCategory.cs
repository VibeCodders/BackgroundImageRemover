namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>One palette group (e.g. "Paint &amp; Retouch") and the tools shown in it, in
/// display order. Built by <see cref="ShellViewModel"/> from the registered <see cref="IToolDefinition"/>
/// set so <see cref="Views.Controls.StrategyToolbar"/> can render the whole palette data-driven.</summary>
public sealed record ToolCategory(string Name, IReadOnlyList<IToolDefinition> Tools);
