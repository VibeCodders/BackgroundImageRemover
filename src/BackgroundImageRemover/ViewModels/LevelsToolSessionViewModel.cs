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
    private double _outputBlack = 0.0;

    [ObservableProperty]
    private double _outputWhite = 255.0;

    [ObservableProperty]
    private bool _autoLevelsEnabled;

    [ObservableProperty]
    private bool _autoWhiteBalanceEnabled;

    [ObservableProperty]
    private bool _equalizeEnabled;

    [ObservableProperty]
    private bool _invertEnabled;

    [ObservableProperty]
    private string? _statusMessage;

    public LevelsToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        RefreshResult();
        StatusMessage = "Adjust black point, white point and gamma.";
    }

    partial void OnBlackPointChanged(double value) => RefreshResult();
    partial void OnWhitePointChanged(double value) => RefreshResult();
    partial void OnGammaChanged(double value) => RefreshResult();
    partial void OnChannelChanged(LevelsChannel value) => RefreshResult();
    partial void OnOutputBlackChanged(double value) => RefreshResult();
    partial void OnOutputWhiteChanged(double value) => RefreshResult();
    partial void OnAutoLevelsEnabledChanged(bool value) => RefreshResult();
    partial void OnAutoWhiteBalanceEnabledChanged(bool value) => RefreshResult();
    partial void OnEqualizeEnabledChanged(bool value) => RefreshResult();
    partial void OnInvertEnabledChanged(bool value) => RefreshResult();

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var result = BuildResult(_sourceImage.FullBgr);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = Math.Abs(BlackPoint) > 1e-4
            || Math.Abs(WhitePoint - 255) > 1e-4
            || Math.Abs(Gamma - 1.0) > 1e-4
            || Math.Abs(OutputBlack) > 1e-4
            || Math.Abs(OutputWhite - 255) > 1e-4
            || AutoLevelsEnabled
            || AutoWhiteBalanceEnabled
            || EqualizeEnabled
            || InvertEnabled;
    }

    private Mat BuildResult(Mat src)
    {
        bool owns = false;
        var current = src;
        current = current.SafeChainWithCatch(r => AutoWhiteBalanceEnabled ? LevelsService.AutoWhiteBalance(r) : r, ref owns);
        current = current.SafeChainWithCatch(r => AutoLevelsEnabled ? LevelsService.AutoLevels(r) : r, ref owns);
        current = current.SafeChainWithCatch(r => LevelsService.Apply(r, BlackPoint, WhitePoint, Gamma, Channel, OutputBlack, OutputWhite), ref owns);
        current = current.SafeChainWithCatch(r => EqualizeEnabled ? LevelsService.Equalize(r) : r, ref owns);
        current = current.SafeChainWithCatch(r => InvertEnabled ? LevelsService.Invert(r) : r, ref owns);
        return current;
    }


    [RelayCommand]
    private void Reset()
    {
        BlackPoint = 0.0;
        WhitePoint = 255.0;
        Gamma = 1.0;
        Channel = LevelsChannel.Rgb;
        OutputBlack = 0.0;
        OutputWhite = 255.0;
        AutoLevelsEnabled = false;
        AutoWhiteBalanceEnabled = false;
        EqualizeEnabled = false;
        InvertEnabled = false;
        RefreshResult();
    }

    public override Task ApplyAsync()
    {
        Mat? bgr = null;
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            bgr = BuildResult(_sourceImage.FullBgr);
        }
        ApplyAndClose(bgr, "Levels");
        return Task.CompletedTask;
    }
}
