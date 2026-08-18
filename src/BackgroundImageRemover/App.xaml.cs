using System.IO;
using System.Windows;
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
using BackgroundImageRemover.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BackgroundImageRemover;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

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

        // Apply the persisted theme and language before the window is shown.
        ThemeManager.Apply(settings.Current.Theme);
        LocalizationService.Instance.Language = settings.Current.Language;

        // Open files passed on the command line (e.g. double-clicking a .ibrproj or an image
        // once the OS associates the extension with this app). Without arguments, optionally
        // reopen the last session's files/projects.
        var startupPaths = e.Args.Where(File.Exists).ToArray();
        window.Loaded += async (_, _) =>
        {
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
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
