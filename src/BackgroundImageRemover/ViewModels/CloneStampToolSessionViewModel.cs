using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class CloneStampToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "🖼 Clone Stamp";
    public override string AccentColor => "#059669";

    [ObservableProperty]
    private double _brushRadius = 20;

    [ObservableProperty]
    private double _opacity = 1;

    [ObservableProperty]
    private double _hardness = 0.8;

    [ObservableProperty]
    private double _sourceX;

    [ObservableProperty]
    private double _sourceY;

    private bool _hasSource;
    private bool _isPainting;
    private WpfPoint _lastPoint;

    public CloneStampToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        SourceX = _sourceImage!.FullBgr.Cols / 2.0;
        SourceY = _sourceImage!.FullBgr.Rows / 2.0;
        _hasSource = false;
        StatusMessage = "Set source point, then paint to clone.";
    }

    public void OnBrushStrokeStart(WpfPoint imagePoint, double pixelRadius)
    {
        if (!_hasSource) return;
        _isPainting = true;
        _lastPoint = imagePoint;
        ApplyClone(imagePoint);
    }

    public void OnBrushStrokeMove(WpfPoint imagePoint, double pixelRadius)
    {
        if (!_isPainting) return;
        ApplyClone(imagePoint);
        _lastPoint = imagePoint;
    }

    public void OnBrushStrokeEnd()
    {
        _isPainting = false;
        ResultBitmap = _sourceImage?.FullBgr.ToBitmapSource(_workingAlpha!);
    }

    private void ApplyClone(WpfPoint destPoint)
    {
        if (!EnsureSourceAlpha() || !_hasSource) return;

        var offset = new Point((int)(SourceX - destPoint.X), (int)(SourceY - destPoint.Y));
        var mask = CreateBrushMaskAt(destPoint, BrushRadius);
        var result = CloneStampService.CloneStamp(_sourceImage!.FullBgr, mask, offset, Opacity, Hardness);
        _sourceImage.FullBgr.Dispose();
        _sourceImage = new LoadedImage(_sourceImage.FilePath, result, _workingAlpha!);
        ResultBitmap = result.ToBitmapSource(_workingAlpha!);
        IsDirty = true;
    }

    public void SetSourcePoint(WpfPoint point)
    {
        SourceX = point.X;
        SourceY = point.Y;
        _hasSource = true;
        StatusMessage = $"Source set at ({point.X:0}, {point.Y:0}). Paint to clone.";
    }

    private Mat CreateBrushMaskAt(WpfPoint center, double radius)
    {
        var mask = new Mat(_sourceImage!.FullBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        int r = (int)Math.Round(radius);
        int x1 = Math.Max(0, (int)center.X - r);
        int y1 = Math.Max(0, (int)center.Y - r);
        int x2 = Math.Min(mask.Cols - 1, (int)center.X + r);
        int y2 = Math.Min(mask.Rows - 1, (int)center.Y + r);

        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                double dx = x - center.X;
                double dy = y - center.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > r) continue;

                double val;
                if (dist <= r * Hardness)
                {
                    val = 1.0;
                }
                else
                {
                    double t = (dist - r * Hardness) / (r * (1 - Hardness));
                    val = 1.0 - t * t;
                }
                mask.Set<byte>(y, x, (byte)Math.Clamp(val * 255, 0, 255));
            }
        }
        return mask;
    }

    protected override void OnReset()
    {
        BrushRadius = 20;
        Opacity = 1;
        Hardness = 0.8;
        _hasSource = false;
        ResultBitmap = _sourceImage?.FullBgr.ToBitmapSource(_workingAlpha!);
    }

    public override async Task ApplyAsync()
    {
        ApplyAndClose(_sourceImage?.FullBgr.Clone(), "CloneStamp");
        await Task.CompletedTask;
    }
}
