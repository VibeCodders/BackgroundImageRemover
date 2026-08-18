using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for levels adjustment (black point, white point, gamma).</summary>
public partial class LevelsToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;

    public override string ToolBadge => "📊 Levels";
    public override string AccentColor => "#B45309";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private double _blackPoint = 0.0;

    [ObservableProperty]
    private double _whitePoint = 255.0;

    [ObservableProperty]
    private double _gamma = 1.0;

    [ObservableProperty]
    private LevelsChannel _channel = LevelsChannel.Rgb;

    [ObservableProperty]
    private string? _statusMessage;

    public LevelsToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        RefreshResult();
        StatusMessage = "Adjust black point, white point and gamma.";
    }

    partial void OnBlackPointChanged(double value) => RefreshResult();
    partial void OnWhitePointChanged(double value) => RefreshResult();
    partial void OnGammaChanged(double value) => RefreshResult();
    partial void OnChannelChanged(LevelsChannel value) => RefreshResult();

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var result = LevelsService.Apply(_sourceImage.FullBgr, BlackPoint, WhitePoint, Gamma, Channel);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = Math.Abs(BlackPoint) > 1e-4 || Math.Abs(WhitePoint - 255) > 1e-4 || Math.Abs(Gamma - 1.0) > 1e-4;
    }

    [RelayCommand]
    private void Reset()
    {
        BlackPoint = 0.0;
        WhitePoint = 255.0;
        Gamma = 1.0;
        Channel = LevelsChannel.Rgb;
        RefreshResult();
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        var bgr = LevelsService.Apply(_sourceImage.FullBgr, BlackPoint, WhitePoint, Gamma, Channel);
        _parentDocument.ApplyToolResult(bgr, _workingAlpha.Clone(), "Levels");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
