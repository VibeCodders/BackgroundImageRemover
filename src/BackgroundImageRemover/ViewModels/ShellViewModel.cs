using System.Collections.ObjectModel;
using System.Linq;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.ComponentModel;
using BackgroundImageRemover.ViewModels.Tools;
using BackgroundImageRemover.ViewModels.Tools.Definitions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Top-level window state: the open document tabs and recent-files list.</summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly Func<DocumentViewModel> _documentFactory;
    private readonly Func<UncropViewModel> _uncropFactory;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;
    private readonly Dictionary<string, IToolDefinition> _toolsById;

    public ObservableCollection<IDocumentTab> Documents { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    /// <summary>The full tool/strategy palette, sorted for display.</summary>
    public IReadOnlyList<IToolDefinition> ToolDefinitions { get; }

    /// <summary>The palette grouped by category, in the toolbar's display order -- <see cref="Views.Controls.StrategyToolbar"/>
    /// binds to this to render its groups instead of hand-listing every tool.</summary>
    public IReadOnlyList<ToolCategory> ToolCategories { get; }

    /// <summary>Fixed left-to-right/top-to-bottom category order for the palette (not alphabetical).</summary>
    private static readonly string[] CategoryDisplayOrder =
    [
        "Background Removal", "Selection", "Paint & Retouch", "Transform",
        "Color & Adjust", "Filters & FX", "Composite", "Text & Decor"
    ];

    [ObservableProperty]
    private IDocumentTab? _selectedDocument;

    public ShellViewModel(
        Func<DocumentViewModel> documentFactory,
        Func<UncropViewModel> uncropFactory,
        IDialogService dialogs,
        ISettingsService settings,
        IDownscaleService downscaler,
        IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy,
        SamStrategy samStrategy,
        IUncropFillService uncropFillService,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IEnumerable<IToolDefinition>? toolDefinitions = null)
    {
        _documentFactory = documentFactory;
        _uncropFactory = uncropFactory;
        _dialogs = dialogs;
        _settings = settings;

        // Production wiring resolves the full registered IToolDefinition set from DI (see
        // App.xaml.cs). Callers that construct ShellViewModel directly (tests) don't have a
        // container to pull that from, so fall back to building the same set from the plain
        // services they already pass in -- no test call site needs to change.
        var tools = (toolDefinitions ?? BuildDefaultToolDefinitions(
            downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy,
            uncropFillService, imageLoader, imageExporter)).ToList();
        _toolsById = tools.ToDictionary(t => t.Id);
        ToolDefinitions = tools
            .Where(t => t.ShowInPalette)
            .OrderBy(t => Array.IndexOf(CategoryDisplayOrder, t.Category))
            .ThenBy(t => t.Order)
            .ToList();
        ToolCategories = ToolDefinitions
            .GroupBy(t => t.Category)
            .Select(g => new ToolCategory(g.Key, g.ToList()))
            .ToList();

        SyncFrom(RecentFiles, _settings.Current.RecentFiles);
        SyncFrom(RecentProjects, _settings.Current.RecentProjects);
    }

    private static IEnumerable<IToolDefinition> BuildDefaultToolDefinitions(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy, IUncropFillService uncropFillService,
        IImageLoaderService imageLoader, IImageExportService imageExporter)
    {
        yield return new RemoveBackgroundToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new OnnxToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new SamToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new GrabCutToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new ChromaKeyToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new MagicWandToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new FloodFillToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new KMeansToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new OtsuToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new InpaintToolDefinition(downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new UncropToolDefinition(uncropFillService, dialogs, imageLoader, imageExporter, log);
        yield return new RetouchToolDefinition();
        yield return new HealToolDefinition();
        yield return new LiquifyToolDefinition();
        yield return new MosaicToolDefinition();
        yield return new CropToolDefinition();
        yield return new TransformToolDefinition();
        yield return new ResizeToolDefinition();
        yield return new RotateToolDefinition();
        yield return new PerspectiveToolDefinition();
        yield return new AdjustmentsToolDefinition(log);
        yield return new LevelsToolDefinition();
        yield return new ColorPickerToolDefinition();
        yield return new BlurToolDefinition();
        yield return new SharpenToolDefinition();
        yield return new VignetteToolDefinition();
        yield return new FiltersToolDefinition();
        yield return new FxToolDefinition();
        yield return new TiltShiftToolDefinition();
        yield return new ComposeToolDefinition(dialogs, imageLoader);
        yield return new OverlayToolDefinition(dialogs, imageLoader);
        yield return new FrameToolDefinition();
        yield return new TextToolDefinition();
        yield return new EmojiToolDefinition();
    }

    /// <summary>
    /// Opens a modal tool session tab for the specified tool.
    /// If a session is already active for this document, focuses it.
    /// </summary>
    public void OpenToolSession(DocumentViewModel doc, EditorTool tool, StrategyKind? initialStrategy = null)
    {
        if (doc.ActiveToolSession is { } existingTab)
        {
            SelectedDocument = existingTab;
            if (initialStrategy is { } activeStrategy && existingTab is BackgroundRemoverToolSessionViewModel bgTab)
            {
                bgTab.SelectedStrategy = activeStrategy;
            }
            return;
        }

        // initialStrategy picks one of the per-strategy palette entries (e.g. clicking the
        // Onnx icon); otherwise fall back to the plain EditorTool dispatch target.
        string id = initialStrategy is { } initialStrategyKind ? $"Strategy.{initialStrategyKind}" : $"Tool.{tool}";
        if (!_toolsById.TryGetValue(id, out var definition))
        {
            return;
        }

        IToolSessionTab toolTab = definition.OpenSession(this, doc);

        doc.ActiveToolSession = toolTab;

        // Insert tool tab right after parent document
        int parentIdx = Documents.IndexOf(doc);
        if (parentIdx >= 0 && parentIdx + 1 <= Documents.Count)
        {
            Documents.Insert(parentIdx + 1, toolTab);
        }
        else
        {
            Documents.Add(toolTab);
        }

        SelectedDocument = toolTab;
    }

    /// <summary>
    /// Closes a tool session directly without prompting.
    /// </summary>
    public virtual void CloseTabDirect(IToolSessionTab toolTab)
    {
        if (toolTab.ParentDocument is { } parent)
        {
            if (parent.ActiveToolSession == toolTab)
            {
                parent.ActiveToolSession = null;
            }
        }

        int index = Documents.IndexOf(toolTab);
        if (index >= 0)
        {
            Documents.RemoveAt(index);
        }
        toolTab.Dispose();

        if (toolTab.ParentDocument is { } targetDoc && Documents.Contains(targetDoc))
        {
            SelectedDocument = targetDoc;
        }
        else if (SelectedDocument == toolTab)
        {
            SelectedDocument = Documents.Count == 0 ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }
    }
}
