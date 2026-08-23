using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>
/// A single independent tool or background-removal strategy in the palette: owns its own
/// metadata (icon, category, tooltip, shortcut) and knows how to activate itself and how to
/// open its dedicated tool session tab. Concrete tools live under Tools/Definitions and are
/// registered in DI (see App.xaml.cs) -- the palette and the tab-opening dispatch are both
/// built purely from the registered set, so adding a tool never requires touching a switch.
/// </summary>
public interface IToolDefinition
{
    /// <summary>Stable key, e.g. "Tool.Retouch" or "Strategy.Onnx".</summary>
    string Id { get; }
    string DisplayName { get; }

    /// <summary>Toolbar grouping, e.g. "Background Removal", "Paint &amp; Retouch".</summary>
    string Category { get; }

    /// <summary>Sort order within <see cref="Category"/>.</summary>
    int Order { get; }

    /// <summary>Key into Themes/StrategyIcons.xaml.</summary>
    string IconResourceKey { get; }
    string ToolTip { get; }
    char? Shortcut { get; }

    /// <summary>False for tools that exist only as a dispatch target (no dedicated palette icon).</summary>
    bool ShowInPalette { get; }

    /// <summary>
    /// True for tools whose left-click <see cref="Select"/> should open their session inline in
    /// the current document tab (GIMP/Photoshop-style, no new tab). False for tools that already
    /// have bespoke inline handling wired directly into <see cref="DocumentViewModel"/> (Uncrop,
    /// Retouch, Adjustments) or that intentionally keep a fully custom <see cref="Select"/> (the
    /// background-removal strategy icons).
    /// </summary>
    bool OpensInlineOnSelect { get; }

    bool IsActive(EditorTool activeTool, StrategyKind selectedStrategy);

    /// <summary>GIMP-style single click: highlights the tool without opening its session tab.</summary>
    void Select(DocumentViewModel doc);

    /// <summary>Middle click / explicit open, invoked from the View layer (which only has a
    /// <see cref="DocumentViewModel"/>, never the <see cref="ShellViewModel"/> directly) --
    /// routes through <see cref="DocumentViewModel"/>'s existing open commands.</summary>
    void RequestOpen(DocumentViewModel doc);

    /// <summary>Middle click / explicit open: spins up the tool's dedicated session tab.
    /// Called by <see cref="ShellViewModel"/> itself once it has decided to open a session.</summary>
    IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc);
}
