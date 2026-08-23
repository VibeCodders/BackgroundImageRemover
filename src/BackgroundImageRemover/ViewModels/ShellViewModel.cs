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
        "Background Removal", "Selection", "Drawing", "Paint & Retouch", "Transform",
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
        // Data-driven set mirroring the DI registrations in App.xaml.cs. Order matters: the
        // palette sort (category + Order) is stable, so ties keep this registration order.
        yield return new ToolDefinition(EditorTool.RemoveBackground, "Remove Background", "Background Removal", -1, "ChromaKeyIcon", "Remove Background",
            (shell, doc) => new BackgroundRemoverToolSessionViewModel(shell, doc, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy, StrategyKind.ChromaKey),
            showInPalette: false);
        yield return new StrategyToolDefinition(StrategyKind.Onnx, 0, "OnnxIcon", "AI Background Removal", "AI Background Removal (ONNX)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.Sam, 1, "SamIcon", "Segment Anything", "Segment Anything (click to select)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.GrabCut, 2, "GrabCutIcon", "GrabCut", "GrabCut (rectangle + scribbles)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.ChromaKey, 3, "ChromaKeyIcon", "Chroma Key", "Chroma Key (solid color backdrop)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.MagicWand, 4, "MagicWandIcon", "Magic Wand", "Magic Wand (click background)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.FloodFill, 5, "FloodFillIcon", "Flood Fill", "Flood Fill (from border)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.KMeans, 6, "KMeansIcon", "K-Means", "K-Means (multi-color backdrop)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.Otsu, 7, "OtsuIcon", "Otsu Threshold", "Otsu Threshold (high contrast)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.Inpaint, 8, "InpaintIcon", "Inpaint", "Inpaint (flood + fill background)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new StrategyToolDefinition(StrategyKind.EdgeContour, 9, "EdgeContourIcon", "Edge / Contour", "Edge / Contour (Canny outline + largest region)", downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy);
        yield return new ToolDefinition(EditorTool.Uncrop, "Uncrop / Expand", "Transform", 4, "UncropIcon", "Uncrop / Expand (U)",
            (shell, doc) => new UncropToolSessionViewModel(shell, doc, uncropFillService, dialogs, imageLoader, imageExporter, log), shortcut: 'U', opensInlineOnSelect: false);
        yield return new ToolDefinition(EditorTool.Retouch, "Retouch & Brush", "Paint & Retouch", 0, "RetouchIcon", "Retouch & Brush (B)", (shell, doc) => new RetouchToolSessionViewModel(shell, doc), shortcut: 'B', opensInlineOnSelect: false);
        yield return new ToolDefinition(EditorTool.Heal, "Heal", "Paint & Retouch", 1, "HealIcon", "Heal (H)", (shell, doc) => new HealToolSessionViewModel(shell, doc), shortcut: 'H');
        yield return new ToolDefinition(EditorTool.Liquify, "Liquify", "Paint & Retouch", 2, "LiquifyIcon", "Liquify (J)", (shell, doc) => new LiquifyToolSessionViewModel(shell, doc), shortcut: 'J');
        yield return new ToolDefinition(EditorTool.Mosaic, "Mosaic", "Paint & Retouch", 3, "MosaicIcon", "Mosaic (M)", (shell, doc) => new MosaicToolSessionViewModel(shell, doc), shortcut: 'M');
        yield return new ToolDefinition(EditorTool.CloneStamp, "Clone Stamp", "Paint & Retouch", 7, "CloneStampIcon", "Clone Stamp (S)", (shell, doc) => new CloneStampToolSessionViewModel(shell, doc), shortcut: 'S');
        yield return new ToolDefinition(EditorTool.ColorReplace, "Color Replace", "Paint & Retouch", 12, "ColorReplaceIcon", "Replace a target color with another color", (shell, doc) => new ColorReplaceToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Crop, "Crop", "Selection", 0, "CropIcon", "Crop (E)", (shell, doc) => new CropToolSessionViewModel(shell, doc), shortcut: 'E');
        yield return new ToolDefinition(EditorTool.LassoSelect, "Lasso Select", "Selection", 1, "LassoIcon", "Lasso Select (freehand outline)", (shell, doc) => new LassoSelectToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Transform, "Transform", "Transform", 0, "TransformIcon", "Transform (T)", (shell, doc) => new TransformToolSessionViewModel(shell, doc), shortcut: 'T');
        yield return new ToolDefinition(EditorTool.Resize, "Resize", "Transform", 1, "ResizeIcon", "Resize (S)", (shell, doc) => new ResizeToolSessionViewModel(shell, doc), shortcut: 'S');
        yield return new ToolDefinition(EditorTool.Rotate, "Rotate", "Transform", 2, "RotateIcon", "Rotate", (shell, doc) => new RotateToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Perspective, "Perspective", "Transform", 3, "PerspectiveIcon", "Perspective (P)", (shell, doc) => new PerspectiveToolSessionViewModel(shell, doc), shortcut: 'P');
        yield return new ToolDefinition(EditorTool.Adjustments, "Adjustments", "Color & Adjust", 0, "AdjustmentsIcon", "Adjustments (A)",
            (shell, doc) => new AdjustmentsToolSessionViewModel(shell, doc, log), shortcut: 'A', opensInlineOnSelect: false);
        yield return new ToolDefinition(EditorTool.Levels, "Levels", "Color & Adjust", 1, "LevelsIcon", "Levels (L)", (shell, doc) => new LevelsToolSessionViewModel(shell, doc), shortcut: 'L');
        yield return new ToolDefinition(EditorTool.ColorPicker, "Color Picker", "Color & Adjust", 2, "ColorPickerIcon", "Color Picker (Q)", (shell, doc) => new ColorPickerToolSessionViewModel(shell, doc), shortcut: 'Q');
        yield return new ToolDefinition(EditorTool.Blur, "Blur", "Color & Adjust", 3, "BlurIcon", "Blur (W)", (shell, doc) => new BlurToolSessionViewModel(shell, doc), shortcut: 'W');
        yield return new ToolDefinition(EditorTool.Sharpen, "Sharpen", "Color & Adjust", 4, "SharpenIcon", "Sharpen (Z)", (shell, doc) => new SharpenToolSessionViewModel(shell, doc), shortcut: 'Z');
        yield return new ToolDefinition(EditorTool.Vignette, "Vignette", "Color & Adjust", 5, "VignetteIcon", "Vignette (V)", (shell, doc) => new VignetteToolSessionViewModel(shell, doc), shortcut: 'V');
        yield return new ToolDefinition(EditorTool.Noise, "Noise", "Color & Adjust", 4, "NoiseIcon", "Add noise (N)", (shell, doc) => new NoiseToolSessionViewModel(shell, doc), shortcut: 'N');
        yield return new ToolDefinition(EditorTool.DodgeBurn, "Dodge / Burn", "Color & Adjust", 5, "DodgeBurnIcon", "Dodge and Burn (B)", (shell, doc) => new DodgeBurnToolSessionViewModel(shell, doc), shortcut: 'B');
        yield return new ToolDefinition(EditorTool.HueSat, "Hue / Sat", "Color & Adjust", 6, "HueSatIcon", "Hue and Saturation (H)", (shell, doc) => new HueSatToolSessionViewModel(shell, doc), shortcut: 'H');
        yield return new ToolDefinition(EditorTool.Duotone, "Duotone", "Color & Adjust", 6, "DuotoneIcon", "Map brightness to a two-color palette", (shell, doc) => new DuotoneToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Filters, "Filters", "Filters & FX", 0, "FiltersIcon", "Filters (F)", (shell, doc) => new FiltersToolSessionViewModel(shell, doc), shortcut: 'F');
        yield return new ToolDefinition(EditorTool.Fx, "FX", "Filters & FX", 1, "FxIcon", "FX (K)", (shell, doc) => new FxToolSessionViewModel(shell, doc), shortcut: 'K');
        yield return new ToolDefinition(EditorTool.TiltShift, "Tilt-Shift", "Filters & FX", 2, "TiltShiftIcon", "Tilt-Shift (I)", (shell, doc) => new TiltShiftToolSessionViewModel(shell, doc), shortcut: 'I');
        yield return new ToolDefinition(EditorTool.Sketch, "Sketch", "Filters & FX", 3, "SketchIcon", "Pencil sketch effect", (shell, doc) => new SketchToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Emboss, "Emboss", "Filters & FX", 4, "EmbossIcon", "Emboss relief effect", (shell, doc) => new EmbossToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Cartoon, "Cartoon", "Filters & FX", 5, "CartoonIcon", "Cartoon look: flat colors and dark outlines", (shell, doc) => new CartoonToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Glow, "Glow", "Filters & FX", 6, "GlowIcon", "Glow around the bright areas", (shell, doc) => new GlowToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Wave, "Wave", "Filters & FX", 7, "WaveIcon", "Wavy (ripple) distortion", (shell, doc) => new WaveToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Thermal, "Thermal", "Filters & FX", 8, "ThermalIcon", "Thermal heatmap palette", (shell, doc) => new ThermalToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.OilPaint, "Oil Paint", "Filters & FX", 9, "OilPaintIcon", "Oil-painting effect", (shell, doc) => new OilPaintToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Halftone, "Halftone", "Filters & FX", 10, "HalftoneIcon", "Halftone dot-matrix rendering", (shell, doc) => new HalftoneToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Bokeh, "Bokeh", "Drawing", 4, "BokehIcon", "Decorative blurred circles", (shell, doc) => new BokehToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Compose, "Compose", "Composite", 0, "ComposeIcon", "Compose (C)",
            (shell, doc) => new ComposeToolSessionViewModel(shell, doc, dialogs, imageLoader), shortcut: 'C');
        yield return new ToolDefinition(EditorTool.Overlay, "Overlay", "Composite", 1, "OverlayIcon", "Overlay (O)",
            (shell, doc) => new OverlayToolSessionViewModel(shell, doc, dialogs, imageLoader), shortcut: 'O');
        yield return new ToolDefinition(EditorTool.Frame, "Frame", "Composite", 2, "FrameIcon", "Frame (G)", (shell, doc) => new FrameToolSessionViewModel(shell, doc), shortcut: 'G');
        yield return new ToolDefinition(EditorTool.Text, "Text", "Text & Decor", 0, "TextIcon", "Text (X)", (shell, doc) => new TextToolSessionViewModel(shell, doc), shortcut: 'X');
        yield return new ToolDefinition(EditorTool.Emoji, "Emoji", "Text & Decor", 1, "EmojiIcon", "Emoji (Y)", (shell, doc) => new EmojiToolSessionViewModel(shell, doc), shortcut: 'Y');
        yield return new ToolDefinition(EditorTool.RedEye, "Red Eye", "Retouch", 8, "RedEyeIcon", "Remove red eyes (R)", (shell, doc) => new RedEyeToolSessionViewModel(shell, doc), shortcut: 'R');
        yield return new ToolDefinition(EditorTool.Shape, "Shape", "Drawing", 2, "ShapeIcon", "Draw a rectangle, ellipse, line or arrow", (shell, doc) => new ShapeToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Gradient, "Gradient", "Drawing", 1, "GradientIcon", "Overlay a linear or radial gradient", (shell, doc) => new GradientToolSessionViewModel(shell, doc));
        yield return new ToolDefinition(EditorTool.Pen, "Pen", "Drawing", 3, "PenIcon", "Draw freehand with a brush/pen", (shell, doc) => new PenToolSessionViewModel(shell, doc));
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
    /// Opens a tool session inline in the current document tab (GIMP/Photoshop-style: left-click
    /// makes the tool immediately usable on the main photo, no new tab). Reuses the exact same
    /// <see cref="IToolDefinition.OpenSession"/> factory as the tab path, just never inserts the
    /// result into <see cref="Documents"/>. Cancels any previously open inline session first, so
    /// there is never more than one live inline session (and its Mats) per document.
    /// </summary>
    public void OpenToolInline(DocumentViewModel doc, EditorTool tool)
    {
        doc.InlineToolSession?.Cancel();

        if (!_toolsById.TryGetValue($"Tool.{tool}", out var definition))
        {
            return;
        }

        doc.InlineToolSession = definition.OpenSession(this, doc);
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
            if (parent.InlineToolSession == toolTab)
            {
                parent.InlineToolSession = null;
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
