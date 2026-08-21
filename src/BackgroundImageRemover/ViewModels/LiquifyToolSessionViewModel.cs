using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for local warps (pinch, bloat, twirl, push).</summary>
public partial class LiquifyToolSessionViewModel : ToolSessionViewModelBase
{
    private Mat? _workingBgra;

    public override string ToolBadge => "✋ Liquify";
    public override string AccentColor => "#9333EA";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private int _centerX;

    [ObservableProperty]
    private int _centerY;

    [ObservableProperty]
    private double _radius = 60;

    [ObservableProperty]
    private double _strength = 0.5;

    [ObservableProperty]
    private LiquifyMode _mode = LiquifyMode.Pinch;

    [ObservableProperty]
    private string? _statusMessage;

    public LiquifyToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgra = _sourceImage!.FullBgr.ToBgra(_workingAlpha!);
        CenterX = _workingBgra.Width / 2;
        CenterY = _workingBgra.Height / 2;
        RefreshResult();
        StatusMessage = "Choose a warp, set center/radius/strength, then apply.";
    }

    [RelayCommand]
    private void ApplyWarp()
    {
        if (_workingBgra is null) return;
        using var warped = LiquifyService.Warp(_workingBgra, new Point(CenterX, CenterY), Radius, Strength, Mode);
        _workingBgra.Dispose();
        _workingBgra = warped.Clone();
        IsDirty = true;
        RefreshResult();
    }

    [RelayCommand]
    private void Reset()
    {
        if (_sourceImage is null) return;
        _workingBgra?.Dispose();
        _workingBgra = _sourceImage.FullBgr.ToBgra(_workingAlpha!);
        CenterX = _workingBgra.Width / 2;
        CenterY = _workingBgra.Height / 2;
        Radius = 60;
        Strength = 0.5;
        Mode = LiquifyMode.Pinch;
        IsDirty = false;
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_workingBgra is null) return;
        ResultBitmap = _workingBgra.ToBitmapSource();
    }

    public override Task ApplyAsync()
    {
        if (_workingBgra is not null)
        {
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(_workingBgra);
            _parentDocument.ApplyToolResult(bgr, alpha, "Liquify");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgra?.Dispose();
        base.Dispose();
    }
}
