using System.ComponentModel;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Common contract for all open document tabs in <see cref="ShellViewModel"/>.
/// </summary>
public interface IDocumentTab : INotifyPropertyChanged, IDisposable
{
    string Title { get; }
    string TabTitle { get; }
    string WindowTitle { get; }
    bool IsDirty { get; }
    string? DirtyHint { get; }
    bool IsCutout { get; }
    string? CutoutHint { get; }
    Task<bool> TrySaveProjectAsync();
}
