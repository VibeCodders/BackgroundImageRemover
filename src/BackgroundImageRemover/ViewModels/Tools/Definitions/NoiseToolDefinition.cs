using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class NoiseToolDefinition : EditorToolDefinitionBase
{
    public NoiseToolDefinition() : base(EditorTool.Noise)
    {
    }

    public override string DisplayName => "Noise";
    public override string Category => "Color & Adjust";
    public override int Order => 4;
    public override string IconResourceKey => "NoiseIcon";
    public override string ToolTip => "Add noise (N)";
    public override char? Shortcut => 'N';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new NoiseToolSessionViewModel(shell, doc);
}
