using System.IO;
using System.Net.Http;
using System.Windows;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Autosave;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Localization;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.ViewModels.Tools;
using BackgroundImageRemover.Views;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace BackgroundImageRemover;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// App-wide service locator for views instantiated by WPF's own data templates (e.g. per-tab
    /// <see cref="Views.DocumentView"/>), which the DI container never constructs directly.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Some GPU/driver combinations (notably NVIDIA Optimus hybrid-graphics laptops) fail to
        // composite WPF's hardware-accelerated bitmap surfaces correctly, rendering loaded images
        // and previews as solid black while vector UI (buttons, text, borders) still draws fine.
        // Forcing software rendering sidesteps the broken hardware path; the app is a 2D image
        // editor, so the performance cost is negligible next to correctness.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        var log = _serviceProvider.GetRequiredService<IFileLogService>();
        DispatcherUnhandledException += (_, args) =>
        {
            log.Error("Unhandled UI exception", args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            log.Error("Unhandled exception", args.ExceptionObject as Exception);
        };

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();
        var autosave = _serviceProvider.GetRequiredService<IAutosaveService>();
        autosave.Start(shell);

        // Apply the persisted theme and language before the window is shown, then watch for
        // Windows light/dark toggles so "Follow Windows" updates live (no restart needed).
        ThemeManager.Apply(settings.Current.Theme);
        ThemeManager.StartWatching();
        LocalizationService.Instance.Language = settings.Current.Language;

        // Open files passed on the command line (e.g. double-clicking a .ibrproj or an image
        // once the OS associates the extension with this app). Without arguments, optionally
        // reopen the last session's files/projects. Any autosave snapshots left by a crashed
        // session are offered for restore first (before other tabs open, so nothing interferes).
        var startupPaths = e.Args.Where(File.Exists).ToArray();
        window.Loaded += async (_, _) =>
        {
            var dialogs = _serviceProvider.GetRequiredService<IDialogService>();
            if (autosave.HasPendingRecovery)
            {
                var entries = autosave.PendingRecovery;
                if (dialogs.ConfirmRestoreRecovery(entries.Count))
                {
                    foreach (var entry in entries)
                    {
                        await shell.OpenInNewTabAsync(entry.FilePath, entry.Title);
                        autosave.RemoveRecoveryEntry(entry.Id);
                    }
                }
                else
                {
                    autosave.DiscardAllRecovery();
                }
            }

            var paths = startupPaths.Length > 0
                ? startupPaths
                : settings.Current.ReopenLastSession
                    ? settings.Current.RecentFiles
                        .Concat(settings.Current.RecentProjects)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(File.Exists)
                        .Take(5)
                    : Array.Empty<string>();

            foreach (var path in paths)
            {
                await shell.OpenInNewTabAsync(path);
            }
        };

        window.Show();
    }

    /// <summary>Builds a data-driven palette entry for one specific Uncrop fill mode.</summary>
    private static IToolDefinition UncropModeTool(
        IServiceProvider sp, EditorTool tool, UncropFillMode fillMode, int order,
        string iconResourceKey, string displayName, string toolTip)
        => new UncropModeToolDefinition(
            tool, fillMode, displayName, order, iconResourceKey, toolTip,
            sp.GetRequiredService<IUncropFillService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<IImageLoaderService>(),
            sp.GetRequiredService<IImageExportService>(),
            sp.GetRequiredService<IFileLogService>(),
            sp.GetService<IAiOutpaintService>());

    /// <summary>Builds a data-driven palette entry for a background-removal strategy.</summary>
    private static IToolDefinition StrategyTool(
        IServiceProvider sp, StrategyKind strategy, int order, string iconResourceKey,
        string displayName, string toolTip)
        => new StrategyToolDefinition(
            strategy, order, iconResourceKey, displayName, toolTip,
            sp.GetRequiredService<IDownscaleService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<IFileLogService>(),
            sp.GetServices<IBackgroundRemovalStrategy>(),
            sp.GetRequiredService<OnnxStrategy>(),
            sp.GetRequiredService<GrabCutStrategy>(),
            sp.GetRequiredService<SamStrategy>());

    private static void ConfigureServices(ServiceCollection services)
    {
        // Model downloads are large (tens to hundreds of MB) and run on first use; a transient
        // network blip would otherwise kill the whole download and force a manual retry. Retry
        // with exponential backoff + jitter (no attempt/total timeouts, which would abort long
        // streams); user cancellation is never retried by Polly.
        services.AddHttpClient<IModelCacheService, ModelCacheService>()
            .AddResilienceHandler("model-download", builder =>
                builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                }));

        services.AddSingleton<IImageLoaderService, ImageLoaderService>();
        services.AddSingleton<IImageExportService, ImageExportService>();
        services.AddSingleton<IDownscaleService, DownscaleService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IBatchProcessingService, BatchProcessingService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IAutosaveService, AutosaveService>();
        services.AddSingleton<IFileLogService, FileLogService>();

        services.AddSingleton<OnnxInferenceEngine>();
        services.AddSingleton<OnnxStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<OnnxStrategy>());
        services.AddSingleton<GrabCutStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<GrabCutStrategy>());
        services.AddSingleton<IBackgroundRemovalStrategy, ChromaKeyStrategy>();
        services.AddSingleton<SamInferenceEngine>();
        services.AddSingleton<SamStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<SamStrategy>());
        services.AddSingleton<FloodFillStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<FloodFillStrategy>());
        services.AddSingleton<KMeansStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<KMeansStrategy>());
        services.AddSingleton<OtsuStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<OtsuStrategy>());
        services.AddSingleton<MagicWandRemovalStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<MagicWandRemovalStrategy>());
        services.AddSingleton<InpaintStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<InpaintStrategy>());
        services.AddSingleton<EdgeContourStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<EdgeContourStrategy>());

        // Tool palette: one IToolDefinition per tool/strategy, registered data-driven from palette
        // metadata + session factories (see ToolDefinition / StrategyToolDefinition). Both the
        // palette (StrategyToolbar) and the tab-opening dispatch (ShellViewModel.OpenToolSession)
        // are built purely from this registered set -- adding a tool is one factory registration.
        services.AddSingleton<IToolDefinition>(sp =>
        {
            var downscaler = sp.GetRequiredService<IDownscaleService>();
            var dialogs = sp.GetRequiredService<IDialogService>();
            var log = sp.GetRequiredService<IFileLogService>();
            var strategies = sp.GetServices<IBackgroundRemovalStrategy>();
            var onnx = sp.GetRequiredService<OnnxStrategy>();
            var grabCut = sp.GetRequiredService<GrabCutStrategy>();
            var sam = sp.GetRequiredService<SamStrategy>();
            return new ToolDefinition(EditorTool.RemoveBackground, "Remove Background", "Background Removal", -1, "ChromaKeyIcon", "Remove Background",
                (shell, doc) => new BackgroundRemoverToolSessionViewModel(shell, doc, downscaler, dialogs, log, strategies, onnx, grabCut, sam, StrategyKind.ChromaKey),
                showInPalette: false);
        });
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.Onnx, 0, "OnnxIcon", "AI Background Removal", "AI Background Removal (ONNX)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.Sam, 1, "SamIcon", "Segment Anything", "Segment Anything (click to select)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.GrabCut, 2, "GrabCutIcon", "GrabCut", "GrabCut (rectangle + scribbles)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.ChromaKey, 3, "ChromaKeyIcon", "Chroma Key", "Chroma Key (solid color backdrop)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.MagicWand, 4, "MagicWandIcon", "Magic Wand", "Magic Wand (click background)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.FloodFill, 5, "FloodFillIcon", "Flood Fill", "Flood Fill (from border)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.KMeans, 6, "KMeansIcon", "K-Means", "K-Means (multi-color backdrop)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.Otsu, 7, "OtsuIcon", "Otsu Threshold", "Otsu Threshold (high contrast)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.Inpaint, 8, "InpaintIcon", "Inpaint", "Inpaint (flood + fill background)"));
        services.AddSingleton<IToolDefinition>(sp => StrategyTool(sp, StrategyKind.EdgeContour, 9, "EdgeContourIcon", "Edge / Contour", "Edge / Contour (Canny outline + largest region)"));
        services.AddSingleton<IToolDefinition>(sp =>
        {
            var dialogs = sp.GetRequiredService<IDialogService>();
            var loader = sp.GetRequiredService<IImageLoaderService>();
            var exporter = sp.GetRequiredService<IImageExportService>();
            var fill = sp.GetRequiredService<IUncropFillService>();
            var log = sp.GetRequiredService<IFileLogService>();
            return new ToolDefinition(EditorTool.Uncrop, "Uncrop / Expand", "Uncrop", 0, "UncropIcon", "Uncrop / Expand (U)",
                (shell, doc) => new UncropToolSessionViewModel(shell, doc, fill, dialogs, loader, exporter, log, initialFillMode: null, aiOutpaintService: sp.GetService<IAiOutpaintService>()), shortcut: 'U');
        });
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropMirror, UncropFillMode.Mirror, 1, "UncropMirrorIcon", "Uncrop Mirror", "Uncrop with a mirror/reflection fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropInpaint, UncropFillMode.Inpaint, 2, "UncropInpaintIcon", "Uncrop Inpaint", "Uncrop with a content-aware inpainting fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropSolidColor, UncropFillMode.SolidColor, 3, "UncropSolidColorIcon", "Uncrop Solid Color", "Uncrop with a solid color fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropReplicate, UncropFillMode.Replicate, 4, "UncropReplicateIcon", "Uncrop Edge Stretch", "Uncrop with an edge-stretch (replicate) fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropWrap, UncropFillMode.Wrap, 5, "UncropWrapIcon", "Uncrop Tile / Wrap", "Uncrop with a tile / wrap fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropZoomBlur, UncropFillMode.ZoomBlur, 6, "UncropZoomBlurIcon", "Uncrop Zoom & Blur", "Uncrop with a zoom & blur background fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropEdgeGradient, UncropFillMode.EdgeGradient, 7, "UncropEdgeGradientIcon", "Uncrop Edge Gradient", "Uncrop with an edge-gradient fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropPatchSynthesis, UncropFillMode.PatchSynthesis, 8, "UncropPatchSynthesisIcon", "Uncrop Patch Synthesis", "Uncrop with patch texture synthesis fill"));
        services.AddSingleton<IToolDefinition>(sp => UncropModeTool(sp, EditorTool.UncropAiOutpaint, UncropFillMode.AiOutpaint, 9, "UncropAiOutpaintIcon", "Uncrop AI (LaMa)", "AI outpainting with LaMa (downloads ~200 MB on first use)"));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Retouch, "Retouch & Brush", "Paint & Retouch", 0, "RetouchIcon", "Retouch & Brush (B)", (shell, doc) => new RetouchToolSessionViewModel(shell, doc), shortcut: 'B'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Heal, "Heal", "Paint & Retouch", 1, "HealIcon", "Heal (H)", (shell, doc) => new HealToolSessionViewModel(shell, doc), shortcut: 'H'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Liquify, "Liquify", "Paint & Retouch", 2, "LiquifyIcon", "Liquify (J)", (shell, doc) => new LiquifyToolSessionViewModel(shell, doc), shortcut: 'J'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Mosaic, "Mosaic", "Paint & Retouch", 3, "MosaicIcon", "Mosaic (M)", (shell, doc) => new MosaicToolSessionViewModel(shell, doc), shortcut: 'M'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.CloneStamp, "Clone Stamp", "Paint & Retouch", 7, "CloneStampIcon", "Clone Stamp (S)", (shell, doc) => new CloneStampToolSessionViewModel(shell, doc), shortcut: 'S'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.ColorReplace, "Color Replace", "Paint & Retouch", 12, "ColorReplaceIcon", "Replace a target color with another color", (shell, doc) => new ColorReplaceToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Crop, "Crop", "Selection", 0, "CropIcon", "Crop (E)", (shell, doc) => new CropToolSessionViewModel(shell, doc), shortcut: 'E'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.LassoSelect, "Lasso Select", "Selection", 1, "LassoIcon", "Lasso Select (freehand outline)", (shell, doc) => new LassoSelectToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Transform, "Transform", "Transform", 0, "TransformIcon", "Transform (T)", (shell, doc) => new TransformToolSessionViewModel(shell, doc), shortcut: 'T'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Resize, "Resize", "Transform", 1, "ResizeIcon", "Resize (S)", (shell, doc) => new ResizeToolSessionViewModel(shell, doc), shortcut: 'S'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Rotate, "Rotate", "Transform", 2, "RotateIcon", "Rotate", (shell, doc) => new RotateToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Perspective, "Perspective", "Transform", 3, "PerspectiveIcon", "Perspective (P)", (shell, doc) => new PerspectiveToolSessionViewModel(shell, doc), shortcut: 'P'));
        services.AddSingleton<IToolDefinition>(sp => new ToolDefinition(EditorTool.Adjustments, "Adjustments", "Color & Adjust", 0, "AdjustmentsIcon", "Adjustments (A)",
            (shell, doc) => new AdjustmentsToolSessionViewModel(shell, doc, sp.GetRequiredService<IFileLogService>()), shortcut: 'A'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Levels, "Levels", "Color & Adjust", 1, "LevelsIcon", "Levels (L)", (shell, doc) => new LevelsToolSessionViewModel(shell, doc), shortcut: 'L'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.ColorPicker, "Color Picker", "Color & Adjust", 2, "ColorPickerIcon", "Color Picker (Q)", (shell, doc) => new ColorPickerToolSessionViewModel(shell, doc), shortcut: 'Q'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Blur, "Blur", "Color & Adjust", 3, "BlurIcon", "Blur (W)", (shell, doc) => new BlurToolSessionViewModel(shell, doc), shortcut: 'W'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Sharpen, "Sharpen", "Color & Adjust", 4, "SharpenIcon", "Sharpen (Z)", (shell, doc) => new SharpenToolSessionViewModel(shell, doc), shortcut: 'Z'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Vignette, "Vignette", "Color & Adjust", 5, "VignetteIcon", "Vignette (V)", (shell, doc) => new VignetteToolSessionViewModel(shell, doc), shortcut: 'V'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Noise, "Noise", "Color & Adjust", 4, "NoiseIcon", "Add noise (N)", (shell, doc) => new NoiseToolSessionViewModel(shell, doc), shortcut: 'N'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.DodgeBurn, "Dodge / Burn", "Color & Adjust", 5, "DodgeBurnIcon", "Dodge and Burn (B)", (shell, doc) => new DodgeBurnToolSessionViewModel(shell, doc), shortcut: 'B'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.HueSat, "Hue / Sat", "Color & Adjust", 6, "HueSatIcon", "Hue and Saturation (H)", (shell, doc) => new HueSatToolSessionViewModel(shell, doc), shortcut: 'H'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Duotone, "Duotone", "Color & Adjust", 6, "DuotoneIcon", "Map brightness to a two-color palette", (shell, doc) => new DuotoneToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Filters, "Filters", "Filters & FX", 0, "FiltersIcon", "Filters (F)", (shell, doc) => new FiltersToolSessionViewModel(shell, doc), shortcut: 'F'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Fx, "FX", "Filters & FX", 1, "FxIcon", "FX (K)", (shell, doc) => new FxToolSessionViewModel(shell, doc), shortcut: 'K'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.TiltShift, "Tilt-Shift", "Filters & FX", 2, "TiltShiftIcon", "Tilt-Shift (I)", (shell, doc) => new TiltShiftToolSessionViewModel(shell, doc), shortcut: 'I'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Sketch, "Sketch", "Filters & FX", 3, "SketchIcon", "Pencil sketch effect", (shell, doc) => new SketchToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Emboss, "Emboss", "Filters & FX", 4, "EmbossIcon", "Emboss relief effect", (shell, doc) => new EmbossToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Cartoon, "Cartoon", "Filters & FX", 5, "CartoonIcon", "Cartoon look: flat colors and dark outlines", (shell, doc) => new CartoonToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Glow, "Glow", "Filters & FX", 6, "GlowIcon", "Glow around the bright areas", (shell, doc) => new GlowToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Wave, "Wave", "Filters & FX", 7, "WaveIcon", "Wavy (ripple) distortion", (shell, doc) => new WaveToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Thermal, "Thermal", "Filters & FX", 8, "ThermalIcon", "Thermal heatmap palette", (shell, doc) => new ThermalToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.OilPaint, "Oil Paint", "Filters & FX", 9, "OilPaintIcon", "Oil-painting effect", (shell, doc) => new OilPaintToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Halftone, "Halftone", "Filters & FX", 10, "HalftoneIcon", "Halftone dot-matrix rendering", (shell, doc) => new HalftoneToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Bokeh, "Bokeh", "Drawing", 4, "BokehIcon", "Decorative blurred circles", (shell, doc) => new BokehToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(sp =>
        {
            var dialogs = sp.GetRequiredService<IDialogService>();
            var loader = sp.GetRequiredService<IImageLoaderService>();
            return new ToolDefinition(EditorTool.Compose, "Compose", "Composite", 0, "ComposeIcon", "Compose (C)",
                (shell, doc) => new ComposeToolSessionViewModel(shell, doc, dialogs, loader), shortcut: 'C');
        });
        services.AddSingleton<IToolDefinition>(sp =>
        {
            var dialogs = sp.GetRequiredService<IDialogService>();
            var loader = sp.GetRequiredService<IImageLoaderService>();
            return new ToolDefinition(EditorTool.Overlay, "Overlay", "Composite", 1, "OverlayIcon", "Overlay (O)",
                (shell, doc) => new OverlayToolSessionViewModel(shell, doc, dialogs, loader), shortcut: 'O');
        });
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Frame, "Frame", "Composite", 2, "FrameIcon", "Frame (G)", (shell, doc) => new FrameToolSessionViewModel(shell, doc), shortcut: 'G'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Text, "Text", "Text & Decor", 0, "TextIcon", "Text (X)", (shell, doc) => new TextToolSessionViewModel(shell, doc), shortcut: 'X'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Emoji, "Emoji", "Text & Decor", 1, "EmojiIcon", "Emoji (Y)", (shell, doc) => new EmojiToolSessionViewModel(shell, doc), shortcut: 'Y'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.RedEye, "Red Eye", "Retouch", 8, "RedEyeIcon", "Remove red eyes (R)", (shell, doc) => new RedEyeToolSessionViewModel(shell, doc), shortcut: 'R'));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Shape, "Shape", "Drawing", 2, "ShapeIcon", "Draw a rectangle, ellipse, line or arrow", (shell, doc) => new ShapeToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Gradient, "Gradient", "Drawing", 1, "GradientIcon", "Overlay a linear or radial gradient", (shell, doc) => new GradientToolSessionViewModel(shell, doc)));
        services.AddSingleton<IToolDefinition>(_ => new ToolDefinition(EditorTool.Pen, "Pen", "Drawing", 3, "PenIcon", "Draw freehand with a brush/pen", (shell, doc) => new PenToolSessionViewModel(shell, doc)));

        services.AddTransient<DocumentViewModel>();
        services.AddSingleton<Func<DocumentViewModel>>(sp => sp.GetRequiredService<DocumentViewModel>);

        services.AddSingleton<IUncropFillService, UncropFillService>();
        services.AddSingleton<ILamaInpaintEngine, LamaInpaintEngine>();
        services.AddSingleton<IAiOutpaintService, AiOutpaintService>();
        services.AddTransient<UncropViewModel>();
        services.AddSingleton<Func<UncropViewModel>>(sp => sp.GetRequiredService<UncropViewModel>);
        services.AddTransient<UncropWindow>();
        services.AddSingleton<Func<UncropWindow>>(sp => sp.GetRequiredService<UncropWindow>);

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The user has confirmed closing all tabs, so no recovery data must survive: leftover
        // snapshots would be misread as a crash on the next launch.
        _serviceProvider?.GetService<IAutosaveService>()?.CleanupOnExit();
        ThemeManager.StopWatching();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
