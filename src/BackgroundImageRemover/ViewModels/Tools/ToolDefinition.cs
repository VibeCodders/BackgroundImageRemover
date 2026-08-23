using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>
/// Data-driven <see cref="IToolDefinition"/> for ordinary tools: all palette metadata is passed
/// to the constructor together with the session factory, so a tool no longer needs an individual
/// class under ViewModels/Tools/Definitions. Tools are registered in <c>App.xaml.cs</c> (and the
/// test fallback in <see cref="ShellViewModel"/>) as one factory registration each.
/// </summary>
public sealed class ToolDefinition : EditorToolDefinitionBase
{
    private readonly string _displayName;
    private readonly string _category;
    private readonly int _order;
    private readonly string _iconResourceKey;
    private readonly string _toolTip;
    private readonly char? _shortcut;
    private readonly bool _showInPalette;
    private readonly bool _opensInlineOnSelect;
    private readonly Func<ShellViewModel, DocumentViewModel, IToolSessionTab> _sessionFactory;

    public ToolDefinition(
        EditorTool tool,
        string displayName,
        string category,
        int order,
        string iconResourceKey,
        string toolTip,
        Func<ShellViewModel, DocumentViewModel, IToolSessionTab> sessionFactory,
        char? shortcut = null,
        bool showInPalette = true,
        bool opensInlineOnSelect = true)
        : base(tool)
    {
        _displayName = displayName;
        _category = category;
        _order = order;
        _iconResourceKey = iconResourceKey;
        _toolTip = toolTip;
        _sessionFactory = sessionFactory;
        _shortcut = shortcut;
        _showInPalette = showInPalette;
        _opensInlineOnSelect = opensInlineOnSelect;
    }

    public override string DisplayName => _displayName;
    public override string Category => _category;
    public override int Order => _order;
    public override string IconResourceKey => _iconResourceKey;
    public override string ToolTip => _toolTip;
    public override char? Shortcut => _shortcut;
    public override bool ShowInPalette => _showInPalette;
    public override bool OpensInlineOnSelect => _opensInlineOnSelect;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => _sessionFactory(shell, doc);
}
