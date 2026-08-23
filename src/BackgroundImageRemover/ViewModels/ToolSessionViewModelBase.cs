using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
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

    // Property names backed by [ToolParameter]-decorated fields, discovered once per concrete type.
    private static readonly ConcurrentDictionary<Type, HashSet<string>> _toolParameterNames = new();

    // Shared snapshot and working-alpha state used by most tool session view models.
    protected LoadedImage? _sourceImage;
    protected Mat? _workingAlpha;

    private DispatcherTimer? _refreshDebounce;
    private CancellationTokenSource? _refreshCts;

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

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private string? _statusMessage;

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

    /// <summary>
    /// Automatically routes any change to a parameter decorated with
    /// <see cref="ToolParameterAttribute"/> into <see cref="OnToolParameterChanged"/>, so tools
    /// no longer need to declare a <c>partial void OnXxxChanged</c> handler for every slider,
    /// check box or color picker. Internal state (title, dirty flag, preview bitmap, status
    /// message) is not marked and therefore does not trigger a refresh.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not null && IsToolParameter(e.PropertyName))
        {
            OnToolParameterChanged();
        }
    }

    /// <summary>
    /// Called after a parameter marked with <see cref="ToolParameterAttribute"/> changes value.
    /// Bases that render a live preview override this to route into their refresh pipeline
    /// (e.g. the debounced <see cref="RequestRefresh"/>); the default is a no-op.
    /// </summary>
    protected virtual void OnToolParameterChanged()
    {
    }

    /// <summary>
    /// True when <paramref name="propertyName"/> is backed by a field (or is a property)
    /// decorated with <see cref="ToolParameterAttribute"/>. The name set is built once per
    /// concrete type (including base-class declarations) and cached.
    /// </summary>
    private bool IsToolParameter(string propertyName)
    {
        var names = _toolParameterNames.GetOrAdd(GetType(), static type =>
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsDefined(typeof(ToolParameterAttribute), false))
                    {
                        names.Add(ToPropertyName(field.Name));
                    }
                }

                foreach (var property in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (property.IsDefined(typeof(ToolParameterAttribute), false))
                    {
                        names.Add(property.Name);
                    }
                }
            }

            return names;
        });
        return names.Contains(propertyName);
    }

    /// <summary>Converts a CommunityToolkit backing-field name (e.g. <c>_blurRadius</c>) to the generated property name (<c>BlurRadius</c>).</summary>
    private static string ToPropertyName(string fieldName)
    {
        var name = fieldName.StartsWith("m_", StringComparison.Ordinal)
            ? fieldName[2..]
            : fieldName.StartsWith("_", StringComparison.Ordinal) ? fieldName[1..] : fieldName;
        return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;
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

    /// <summary>Shared "↺ Reset" command: restores the tool's default parameters.</summary>
    [RelayCommand]
    private void Reset() => OnReset();

    /// <summary>
    /// Restores default parameter values and refreshes the preview. Tool view models override
    /// this with their own parameter defaults; the base implementation is a no-op.
    /// </summary>
    protected virtual void OnReset()
    {
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

    protected bool EnsureSourceAlpha()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Coalesces rapid parameter changes (slider drags, color picker spins) into a single
    /// asynchronous refresh: the debounce timer restarts on every call and only fires after the
    /// value settles, so a drag runs the full-resolution effect once instead of once per tick
    /// on the UI thread (which froze the UI while dragging). Subclasses override
    /// <see cref="RefreshAsync"/> to do the actual work.
    /// </summary>
    protected void RequestRefresh()
    {
        if (_refreshDebounce is null)
        {
            _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _refreshDebounce.Tick += (_, _) => _ = RefreshCoreAsync();
        }
        _refreshDebounce.Stop();
        _refreshDebounce.Start();
    }

    private async Task RefreshCoreAsync()
    {
        _refreshDebounce?.Stop();
        await RefreshAsync();
    }

    /// <summary>
    /// Runs the tool's debounced preview refresh. The base implementation is a no-op; the
    /// effect-tool bases (and the Adjustments tool) override it with their own compute.
    /// </summary>
    protected virtual Task RefreshAsync() => Task.CompletedTask;

    /// <summary>Cancels any in-flight asynchronous refresh.</summary>
    protected void CancelRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    /// <summary>
    /// Starts a new refresh generation (cancelling the previous one) and returns its token, so
    /// a superseded run never overwrites a newer result. Call from <see cref="RefreshAsync"/>.
    /// </summary>
    protected CancellationToken BeginRefresh()
    {
        CancelRefresh();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        return cts.Token;
    }

    /// <summary>
    /// Clones the full-resolution BGR source into an independent mutable working copy.
    /// Callers must own and dispose the returned Mat.
    /// </summary>
    protected Mat CloneWorkingBgr() => _sourceImage!.FullBgr.Clone();

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

    /// <summary>
    /// Splits a BGRA result into BGR + alpha and applies both to the parent document (ownership
    /// of the split Mats transfers to the document). The tool tab stays open — call
    /// <see cref="ApplyBgraAndClose"/> when the tab should close too.
    /// </summary>
    protected void ApplyBgra(Mat bgra, string operationName)
    {
        var (bgr, alpha) = BackgroundCompositingService.SplitBgra(bgra);
        _parentDocument.ApplyToolResult(bgr, alpha, operationName);
    }

    /// <summary>Splits a BGRA result, applies it to the parent document and closes the tool tab.</summary>
    protected void ApplyBgraAndClose(Mat bgra, string operationName)
    {
        ApplyBgra(bgra, operationName);
        _shell.CloseTabDirect(this);
    }

    /// <summary>
    /// Captures the current document state and returns an independent mutable clone of the
    /// full-resolution BGR, ready to be assigned to the tool's working copy. Callers own the
    /// returned Mat. Eliminates the duplicated init + clone pair in the working-copy tools.
    /// </summary>
    protected Mat CloneSourceWorkingBgr()
    {
        InitSourceAlpha();
        return CloneWorkingBgr();
    }

    public virtual void Dispose()
    {
        _refreshDebounce?.Stop();
        CancelRefresh();
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
