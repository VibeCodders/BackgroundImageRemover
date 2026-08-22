using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Abstract base for tools that apply an effect either to the whole image or to a painted mask region.
/// Encapsulates the common pattern: mask painting, preview refresh, apply/cancel lifecycle.
/// </summary>
public abstract partial class MaskToolSessionViewModelBase : ToolSessionViewModelBase, ITool
{
    private readonly BrushStrokeController _strokes = new();

    protected Mat? _paintedMask;

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private double _brushRadius = 40;

    [ObservableProperty]
    private bool _wholeImage;

    [ObservableProperty]
    private bool _paintMode;

    protected MaskToolSessionViewModelBase(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
    }

    /// <summary>Initializes the source snapshot, working alpha, and painted mask. Call from subclass constructor.</summary>
    protected void InitMask()
    {
        InitSourceAlpha();
        _paintedMask = new Mat(_sourceImage!.FullBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha!);
        RefreshResult();
    }

    /// <summary>True when the painted mask has any non-zero pixels.</summary>
    protected bool HasPaintedMask => _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0;

    protected bool IsEffectActive => WholeImage || (PaintMode && HasPaintedMask);

    /// <summary>Gets the painted mask, or null if none exists.</summary>
    protected Mat? PaintedMask => _paintedMask;

    public void OnBrushStrokeStart(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Begin(imagePoint, pixelRadius, StampMask);

    public void OnBrushStrokeMove(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Extend(imagePoint, pixelRadius, StampMask);

    public virtual void OnBrushStrokeEnd()
    {
        _strokes.End();
        RefreshResult();
    }

    private void StampMask(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_paintedMask is null) return;
        MaskBrushHelper.StampSegment(_paintedMask, from, to, pixelRadius);
    }

    [RelayCommand]
    private void ClearMask()
    {
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }

    /// <summary>
    /// Builds the preview result. Called on every parameter change.
/// </summary>
    protected abstract void RefreshResult();

    /// <summary>
    /// Builds the final result Mat for application. Called when the user applies the tool.
    /// </summary>
    /// <param name="src">The source BGR image.</param>
    /// <returns>The resulting BGR image (caller owns disposal).</returns>
    protected abstract Mat BuildResult(Mat src);

    /// <summary>
    /// Gets the operation name to record in the edit history.
    /// </summary>
    protected abstract string OperationName { get; }

    public override Task ApplyAsync()
    {
        ApplyAndClose(_sourceImage is not null && _workingAlpha is not null ? BuildResult(_sourceImage.FullBgr) : null, OperationName);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
        _paintedMask?.Dispose();
    }
}
