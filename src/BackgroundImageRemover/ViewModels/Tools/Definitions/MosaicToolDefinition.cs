using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class MosaicToolDefinition : EditorToolDefinitionBase
{
    public MosaicToolDefinition() : base(EditorTool.Mosaic)
    {
    }

    public override string DisplayName => "Mosaic";
    public override string Category => "Paint & Retouch";
    public override int Order => 3;
    public override string IconResourceKey => "MosaicIcon";
    public override string ToolTip => "Mosaic (M)";
    public override char? Shortcut => 'M';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new MosaicToolSessionViewModel(shell, doc);
}
