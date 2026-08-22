using System.IO;
using System.Windows;
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
using BackgroundImageRemover.ViewModels.Tools.Definitions;
using BackgroundImageRemover.Views;
using Microsoft.Extensions.DependencyInjection;

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

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddHttpClient<IModelCacheService, ModelCacheService>();

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

        // Tool palette: one independent IToolDefinition per tool/strategy. Adding a tool means
        // adding one class under ViewModels/Tools/Definitions and one line here -- both the
        // palette (StrategyToolbar) and the tab-opening dispatch (ShellViewModel.OpenToolSession)
        // are built purely from this registered set.
        services.AddSingleton<IToolDefinition, RemoveBackgroundToolDefinition>();
        services.AddSingleton<IToolDefinition, OnnxToolDefinition>();
        services.AddSingleton<IToolDefinition, SamToolDefinition>();
        services.AddSingleton<IToolDefinition, GrabCutToolDefinition>();
        services.AddSingleton<IToolDefinition, ChromaKeyToolDefinition>();
        services.AddSingleton<IToolDefinition, MagicWandToolDefinition>();
        services.AddSingleton<IToolDefinition, FloodFillToolDefinition>();
        services.AddSingleton<IToolDefinition, KMeansToolDefinition>();
        services.AddSingleton<IToolDefinition, OtsuToolDefinition>();
        services.AddSingleton<IToolDefinition, InpaintToolDefinition>();
        services.AddSingleton<IToolDefinition, EdgeContourToolDefinition>();
        services.AddSingleton<IToolDefinition, UncropToolDefinition>();
        services.AddSingleton<IToolDefinition, RetouchToolDefinition>();
        services.AddSingleton<IToolDefinition, HealToolDefinition>();
        services.AddSingleton<IToolDefinition, LiquifyToolDefinition>();
        services.AddSingleton<IToolDefinition, MosaicToolDefinition>();
        services.AddSingleton<IToolDefinition, CropToolDefinition>();
        services.AddSingleton<IToolDefinition, LassoSelectToolDefinition>();
        services.AddSingleton<IToolDefinition, TransformToolDefinition>();
        services.AddSingleton<IToolDefinition, ResizeToolDefinition>();
        services.AddSingleton<IToolDefinition, RotateToolDefinition>();
        services.AddSingleton<IToolDefinition, PerspectiveToolDefinition>();
        services.AddSingleton<IToolDefinition, AdjustmentsToolDefinition>();
        services.AddSingleton<IToolDefinition, LevelsToolDefinition>();
        services.AddSingleton<IToolDefinition, ColorPickerToolDefinition>();
        services.AddSingleton<IToolDefinition, BlurToolDefinition>();
        services.AddSingleton<IToolDefinition, SharpenToolDefinition>();
        services.AddSingleton<IToolDefinition, VignetteToolDefinition>();
        services.AddSingleton<IToolDefinition, FiltersToolDefinition>();
        services.AddSingleton<IToolDefinition, FxToolDefinition>();
        services.AddSingleton<IToolDefinition, TiltShiftToolDefinition>();
        services.AddSingleton<IToolDefinition, ComposeToolDefinition>();
        services.AddSingleton<IToolDefinition, OverlayToolDefinition>();
        services.AddSingleton<IToolDefinition, FrameToolDefinition>();
        services.AddSingleton<IToolDefinition, TextToolDefinition>();
        services.AddSingleton<IToolDefinition, EmojiToolDefinition>();
        services.AddSingleton<IToolDefinition, NoiseToolDefinition>();
        services.AddSingleton<IToolDefinition, DodgeBurnToolDefinition>();
        services.AddSingleton<IToolDefinition, HueSatToolDefinition>();
        services.AddSingleton<IToolDefinition, CloneStampToolDefinition>();
        services.AddSingleton<IToolDefinition, RedEyeToolDefinition>();
        services.AddSingleton<IToolDefinition, ShapeToolDefinition>();
        services.AddSingleton<IToolDefinition, GradientToolDefinition>();
        services.AddSingleton<IToolDefinition, ColorReplaceToolDefinition>();
        services.AddSingleton<IToolDefinition, DuotoneToolDefinition>();

        services.AddTransient<DocumentViewModel>();
        services.AddSingleton<Func<DocumentViewModel>>(sp => sp.GetRequiredService<DocumentViewModel>);

        services.AddSingleton<IUncropFillService, UncropFillService>();
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
