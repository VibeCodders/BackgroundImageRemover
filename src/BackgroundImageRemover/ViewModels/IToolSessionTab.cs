namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Represents a temporary modal-like tool session tab opened from a parent <see cref="DocumentViewModel"/>.
/// </summary>
public interface IToolSessionTab : IDocumentTab
{
    /// <summary>The parent document that spawned this tool session.</summary>
    DocumentViewModel ParentDocument { get; }

    /// <summary>User friendly name or icon of the active tool (e.g. "✂ Remove Background").</summary>
    string ToolBadge { get; }

    /// <summary>Color brush or hex string used for visual identification of this tool tab.</summary>
    string AccentColor { get; }

    /// <summary>Cancels the tool session without saving.</summary>
    void Cancel();
}
