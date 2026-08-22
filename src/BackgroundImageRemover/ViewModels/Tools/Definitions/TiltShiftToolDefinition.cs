using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class TiltShiftToolDefinition : EditorToolDefinitionBase
{
    public TiltShiftToolDefinition() : base(EditorTool.TiltShift)
    {
    }

    public override string DisplayName => "Tilt-Shift";
    public override string Category => "Filters & FX";
    public override int Order => 2;
    public override string IconResourceKey => "TiltShiftIcon";
    public override string ToolTip => "Tilt-Shift (I)";
    public override char? Shortcut => 'I';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new TiltShiftToolSessionViewModel(shell, doc);
}
