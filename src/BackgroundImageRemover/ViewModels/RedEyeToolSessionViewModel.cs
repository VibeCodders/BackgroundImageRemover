using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class RedEyeToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "👁 Red Eye";
    public override string AccentColor => "#DC2626";

    [ObservableProperty]
    private double _radius = 15;

    public RedEyeToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        StatusMessage = "Click on a red eye to remove it.";
    }

    partial void OnRadiusChanged(double value) => RefreshResult();

    public void OnClick(WpfPoint imagePoint)
    {
        if (!EnsureSourceAlpha()) return;

        var center = new Point((int)imagePoint.X, (int)imagePoint.Y);
        var result = RedEyeService.RemoveRedEyes(_sourceImage!.FullBgr, center, Radius);
        _sourceImage.FullBgr.Dispose();
        _sourceImage = new LoadedImage(_sourceImage.FilePath, result, _workingAlpha!);
        ResultBitmap = result.ToBitmapSource(_workingAlpha!);
        IsDirty = true;
        StatusMessage = "Red eye removed.";
    }

    private void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;
        ResultBitmap = _sourceImage!.FullBgr.ToBitmapSource(_workingAlpha!);
    }

    [RelayCommand]
    private void Reset()
    {
        Radius = 15;
        RefreshResult();
    }

    public override async Task ApplyAsync()
    {
        ApplyAndClose(_sourceImage?.FullBgr.Clone(), "RedEye");
        await Task.CompletedTask;
    }
}
