using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for freehand (lasso) selection: draw an outline on the canvas and keep
/// (or, inverted, drop) everything inside it. The outline is closed and filled into a mask on
/// mouse-up (<see cref="OnStrokeEnd"/>); each new drag redefines the selection from scratch.
/// </summary>
public partial class LassoSelectToolSessionViewModel : BgraToolSessionViewModelBase, IBrushStrokeSession
{
    private readonly List<Point> _points = new();

    public override string ToolBadge => "◈ Lasso";
    public override string AccentColor => "#0EA5E9";

    [ObservableProperty]
    private bool _invertSelection;

    [ObservableProperty]
    private int _featherPixels;

    [ObservableProperty]
    private bool _hasSelection;

    public LassoSelectToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitWorkingBgra();
        ResultBitmap = SourceBitmap;
        StatusMessage = "Drag on the left to draw a freehand selection outline.";
    }

    // Lasso has no brush radius; the shared handlers still pass one, which is ignored here.
    double IBrushStrokeSession.BrushRadius => 0;

    public void OnStrokeStart(WpfPoint p, double pixelRadius)
    {
        _points.Clear();
        _points.Add(ToCvPoint(p));
    }

    public void OnStrokeMove(WpfPoint p, double pixelRadius)
    {
        var next = ToCvPoint(p);
        if (_points.Count > 0)
        {
            var last = _points[^1];
            // Skip near-duplicate points so the polygon stays light without losing shape.
            if (Math.Abs(last.X - next.X) < 2 && Math.Abs(last.Y - next.Y) < 2)
            {
                return;
            }
        }
        _points.Add(next);
    }

    public void OnStrokeEnd()
    {
        if (WorkingBgra is null || _points.Count < 3)
        {
            StatusMessage = "Drag a longer outline to select a region.";
            return;
        }

        HasSelection = true;
        IsDirty = true;
        RefreshResult();
        StatusMessage = "Selection drawn. Apply to cut it out, or drag again to redraw.";
    }

    partial void OnInvertSelectionChanged(bool value) => RefreshResult();
    partial void OnFeatherPixelsChanged(int value) => RefreshResult();

    [RelayCommand]
    private void ClearSelection()
    {
        _points.Clear();
        HasSelection = false;
        IsDirty = false;
        ResultBitmap = SourceBitmap;
        StatusMessage = "Selection cleared. Drag to draw again.";
    }

    private Mat? BuildMask()
    {
        if (WorkingBgra is null || _points.Count < 3)
        {
            return null;
        }

        var mask = new Mat(WorkingBgra.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.FillPoly(mask, new[] { _points.ToArray() }, Scalar.All(255));

        if (FeatherPixels > 0)
        {
            int kernelSize = FeatherPixels * 2 + 1;
            Cv2.GaussianBlur(mask, mask, new Size(kernelSize, kernelSize), 0);
        }

        if (InvertSelection)
        {
            Cv2.BitwiseNot(mask, mask);
        }

        return mask;
    }

    private void RefreshResult()
    {
        if (WorkingBgra is null)
        {
            return;
        }

        using var mask = BuildMask();
        if (mask is null)
        {
            ResultBitmap = SourceBitmap;
            return;
        }

        using var bgr = new Mat();
        Cv2.CvtColor(WorkingBgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var resultBgra = bgr.ToBgra(mask);
        ResultBitmap = resultBgra.ToBitmapSource();
    }

    private static Point ToCvPoint(WpfPoint p) => new((int)Math.Round(p.X), (int)Math.Round(p.Y));

    public override Task ApplyAsync()
    {
        if (WorkingBgra is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        using var mask = BuildMask();
        if (mask is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        using var bgr = new Mat();
        Cv2.CvtColor(WorkingBgra, bgr, ColorConversionCodes.BGRA2BGR);
        _parentDocument.ApplyToolResult(bgr, mask.Clone(), "Lasso Select");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }
}
