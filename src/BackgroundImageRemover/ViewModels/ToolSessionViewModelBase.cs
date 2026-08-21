using System.ComponentModel;
using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Abstract base class for temporary tool workspace tabs opened from a parent <see cref="DocumentViewModel"/>.
/// </summary>
public abstract partial class ToolSessionViewModelBase : ObservableObject, IToolSessionTab, ITool
{
    protected readonly ShellViewModel _shell;
    protected readonly DocumentViewModel _parentDocument;

    // Shared snapshot and working-alpha state used by most tool session view models.
    protected LoadedImage? _sourceImage;
    protected Mat? _workingAlpha;

    public DocumentViewModel ParentDocument => _parentDocument;

    public abstract string ToolBadge { get; }
    public abstract string AccentColor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirtyHint))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private bool _isDirty;

    public string? DirtyHint => IsDirty ? "Unapplied changes in tool session." : null;
    public virtual bool IsCutout => false;
    public virtual string? CutoutHint => null;
    public string WindowTitle => $"{ToolBadge} — {Title}{(IsDirty ? " *" : string.Empty)}";
    public string TabTitle => $"{ToolBadge} {Title}{(IsDirty ? " *" : string.Empty)}";

    protected ToolSessionViewModelBase(ShellViewModel shell, DocumentViewModel parentDocument)
    {
        _shell = shell;
        _parentDocument = parentDocument;
        _title = Path.GetFileName(parentDocument.Title);
        if (string.IsNullOrWhiteSpace(_title))
        {
            _title = "Untitled";
        }
    }

    public virtual Task<bool> TrySaveProjectAsync() => Task.FromResult(true);

    /// <summary>
    /// Executes when user presses the Apply button or Enter key.
    /// Closes the tool tab and applies resulting image to parent document.
    /// </summary>
    [RelayCommand]
    public abstract Task ApplyAsync();

    /// <summary>
    /// Executes when user presses the Cancel button, Esc key or tab close button.
    /// Discards changes and returns to parent document.
    /// </summary>
    [RelayCommand]
    public virtual void Cancel()
    {
        _shell.CloseTabDirect(this);
    }

    /// <summary>
    /// Captures the current document state into <see cref="_sourceImage"/> and initialises
    /// <see cref="_workingAlpha"/> from it (cloned alpha, or a fully-opaque Mat if the source
    /// has no alpha channel). Eliminates the duplicated snapshot + alpha boilerplate in subclasses.
    /// </summary>
    protected void InitSourceAlpha()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.GetWorkingAlpha();
    }

    /// <summary>
    /// Applies <paramref name="bgr"/> to the parent document together with a cloned working alpha,
    /// then closes the tool tab. If <paramref name="bgr"/> or the working alpha is null the tab
    /// is closed without applying. Eliminates the duplicated ApplyAsync + CloseTab boilerplate.
    /// </summary>
    protected void ApplyAndClose(Mat? bgr, string operationName)
    {
        if (bgr is not null && _workingAlpha is not null)
        {
            _parentDocument.ApplyToolResult(bgr, _workingAlpha.Clone(), operationName);
        }
        _shell.CloseTabDirect(this);
    }

    public virtual void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
