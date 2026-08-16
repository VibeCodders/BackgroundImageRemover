using System.Windows;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Preview;
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

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddHttpClient<IModelCacheService, ModelCacheService>();

        services.AddSingleton<IImageLoaderService, ImageLoaderService>();
        services.AddSingleton<IImageExportService, ImageExportService>();
        services.AddSingleton<IDownscaleService, DownscaleService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<OnnxInferenceEngine>();
        services.AddSingleton<OnnxU2NetStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy>(sp => sp.GetRequiredService<OnnxU2NetStrategy>());
        services.AddSingleton<IBackgroundRemovalStrategy, GrabCutStrategy>();
        services.AddSingleton<IBackgroundRemovalStrategy, ChromaKeyStrategy>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
