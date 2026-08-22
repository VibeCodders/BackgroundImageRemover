using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class CropToolDefinition : EditorToolDefinitionBase
{
    public CropToolDefinition() : base(EditorTool.Crop)
    {
    }

    public override string DisplayName => "Crop";
    public override string Category => "Selection";
    public override int Order => 0;
    public override string IconResourceKey => "CropIcon";
    public override string ToolTip => "Crop (E)";
    public override char? Shortcut => 'E';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new CropToolSessionViewModel(shell, doc);
}
