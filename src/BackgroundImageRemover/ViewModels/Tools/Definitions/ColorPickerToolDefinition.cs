using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class ColorPickerToolDefinition : EditorToolDefinitionBase
{
    public ColorPickerToolDefinition() : base(EditorTool.ColorPicker)
    {
    }

    public override string DisplayName => "Color Picker";
    public override string Category => "Color & Adjust";
    public override int Order => 2;
    public override string IconResourceKey => "ColorPickerIcon";
    public override string ToolTip => "Color Picker (Q)";
    public override char? Shortcut => 'Q';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new ColorPickerToolSessionViewModel(shell, doc);
}
