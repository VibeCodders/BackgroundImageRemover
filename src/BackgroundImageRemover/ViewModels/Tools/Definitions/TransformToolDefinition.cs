using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class TransformToolDefinition : EditorToolDefinitionBase
{
    public TransformToolDefinition() : base(EditorTool.Transform)
    {
    }

    public override string DisplayName => "Transform";
    public override string Category => "Transform";
    public override int Order => 0;
    public override string IconResourceKey => "TransformIcon";
    public override string ToolTip => "Transform (T)";
    public override char? Shortcut => 'T';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new TransformToolSessionViewModel(shell, doc);
}
