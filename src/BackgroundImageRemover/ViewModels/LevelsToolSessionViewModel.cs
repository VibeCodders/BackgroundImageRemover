using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for levels adjustment (black point, white point, gamma).</summary>
public partial class LevelsToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "📊 Levels";
    public override string AccentColor => "#B45309";

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

    protected override string OperationName => "Levels";

    protected override bool IsEffectActive =>
        Math.Abs(BlackPoint) > 1e-4
        || Math.Abs(WhitePoint - 255) > 1e-4
        || Math.Abs(Gamma - 1.0) > 1e-4
        || Math.Abs(OutputBlack) > 1e-4
        || Math.Abs(OutputWhite - 255) > 1e-4
        || AutoLevelsEnabled
        || AutoWhiteBalanceEnabled
        || EqualizeEnabled
        || InvertEnabled;

    public LevelsToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Adjust black point, white point and gamma.")
    {
        RefreshPreview();
    }

    partial void OnBlackPointChanged(double value) => RefreshPreview();
    partial void OnWhitePointChanged(double value) => RefreshPreview();
    partial void OnGammaChanged(double value) => RefreshPreview();
    partial void OnChannelChanged(LevelsChannel value) => RefreshPreview();
    partial void OnOutputBlackChanged(double value) => RefreshPreview();
    partial void OnOutputWhiteChanged(double value) => RefreshPreview();
    partial void OnAutoLevelsEnabledChanged(bool value) => RefreshPreview();
    partial void OnAutoWhiteBalanceEnabledChanged(bool value) => RefreshPreview();
    partial void OnEqualizeEnabledChanged(bool value) => RefreshPreview();
    partial void OnInvertEnabledChanged(bool value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
    {
        bool owns = false;
        var current = bgr;
        current = current.SafeChainWithCatch(r => AutoWhiteBalanceEnabled ? LevelsService.AutoWhiteBalance(r) : r, ref owns);
        current = current.SafeChainWithCatch(r => AutoLevelsEnabled ? LevelsService.AutoLevels(r) : r, ref owns);
        current = current.SafeChainWithCatch(r => LevelsService.Apply(r, BlackPoint, WhitePoint, Gamma, Channel, OutputBlack, OutputWhite), ref owns);
        current = current.SafeChainWithCatch(r => EqualizeEnabled ? LevelsService.Equalize(r) : r, ref owns);
        current = current.SafeChainWithCatch(r => InvertEnabled ? LevelsService.Invert(r) : r, ref owns);
        return current;
    }

    protected override void OnResetDefaults()
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
    }
}
