using System.Windows.Media.Imaging;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Common contract for all editing tools in the application.
/// Both full tool sessions and inline tools implement this interface.
/// </summary>
public interface ITool
{
    /// <summary>Display name for the tool badge in the tab header.</summary>
    string ToolBadge { get; }

    /// <summary>Accent color (hex) for the tool's visual identity.</summary>
    string AccentColor { get; }

    /// <summary>The parent document this tool operates on.</summary>
    DocumentViewModel ParentDocument { get; }

    /// <source>The current title of the tool session.</source>
    string Title { get; }

    /// <summary>Whether the tool has unapplied changes.</summary>
    bool IsDirty { get; }

    /// <summary>Applies the tool's result to the parent document and closes the tool.</summary>
    Task ApplyAsync();

    /// <summary>Discards changes and closes the tool.</summary>
    void Cancel();
}
