using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>Base for tools identified by a single <see cref="EditorTool"/> value.</summary>
public abstract class EditorToolDefinitionBase : IToolDefinition
{
    protected EditorToolDefinitionBase(EditorTool tool) => Tool = tool;

    public EditorTool Tool { get; }
    public string Id => $"Tool.{Tool}";
    public abstract string DisplayName { get; }
    public abstract string Category { get; }
    public abstract int Order { get; }
    public abstract string IconResourceKey { get; }
    public abstract string ToolTip { get; }
    public abstract char? Shortcut { get; }
    public virtual bool ShowInPalette => true;

    public virtual bool OpensInlineOnSelect => true;

    public bool IsActive(EditorTool activeTool, StrategyKind selectedStrategy) => activeTool == Tool;

    public void Select(DocumentViewModel doc)
    {
        doc.ActiveTool = Tool;
        if (OpensInlineOnSelect)
        {
            doc.OpenToolInlineCommand.Execute(Tool);
        }
    }

    public void RequestOpen(DocumentViewModel doc) => doc.OpenToolTabCommand.Execute(Tool);

    public abstract IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc);
}
