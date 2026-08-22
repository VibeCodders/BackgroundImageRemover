using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class HueSatToolDefinition : EditorToolDefinitionBase
{
    public HueSatToolDefinition() : base(EditorTool.HueSat)
    {
    }

    public override string DisplayName => "Hue / Sat";
    public override string Category => "Color & Adjust";
    public override int Order => 6;
    public override string IconResourceKey => "HueSatIcon";
    public override string ToolTip => "Hue and Saturation (H)";
    public override char? Shortcut => 'H';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new HueSatToolSessionViewModel(shell, doc);
}
