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
public abstract partial class MaskToolSessionViewModelBase : ToolSessionViewModelBase, ITool, IBrushStrokeSession
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

    /// <summary>True when the painted mask has any non-zero pixels (raises change notification after painting).</summary>
    public bool HasPaintedMask => _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0;

    protected bool IsEffectActive => WholeImage || (PaintMode && HasPaintedMask);

    /// <summary>Gets the painted mask, or null if none exists.</summary>
    protected Mat? PaintedMask => _paintedMask;

    public void OnStrokeStart(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Begin(imagePoint, pixelRadius, StampMask);

    public void OnStrokeMove(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Extend(imagePoint, pixelRadius, StampMask);

    public virtual void OnStrokeEnd()
    {
        _strokes.End();
        OnPropertyChanged(nameof(HasPaintedMask));
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
        OnPropertyChanged(nameof(HasPaintedMask));
        RefreshResult();
    }

    /// <summary>
    /// Builds the preview result and updates the bitmap. Called on every parameter change.
    /// Concrete here: subclasses only implement <see cref="ApplyEffect"/> (and optionally
    /// <see cref="ApplyEffectToRegion"/>), so the whole-image / painted-mask / unchanged
    /// branching lives in one place instead of being copy-pasted in every mask tool.
    /// </summary>
    protected virtual void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;
        using var result = BuildResult(_sourceImage!.FullBgr);
        ResultBitmap = result.ToBitmapSource(_workingAlpha!);
        IsDirty = IsEffectActive;
    }

    /// <summary>
    /// Builds the final result Mat: applies the effect to the whole image, to the painted mask
    /// region, or returns an unchanged clone. Called when the user applies the tool.
    /// </summary>
    /// <param name="src">The source BGR image.</param>
    /// <returns>The resulting BGR image (caller owns disposal).</returns>
    protected virtual Mat BuildResult(Mat src)
    {
        if (WholeImage)
        {
            return ApplyEffect(src);
        }
        if (PaintMode && HasPaintedMask)
        {
            return ApplyEffectToRegion(src, _paintedMask!);
        }
        return src.Clone();
    }

    /// <summary>Applies the tool's effect to the entire image.</summary>
    protected abstract Mat ApplyEffect(Mat src);

    /// <summary>
    /// Applies the tool's effect restricted to the painted mask. The default (whole-image effect
    /// blended back by the mask) matches the standard mask-tool semantics; tools with dedicated
    /// region implementations override this.
    /// </summary>
    protected virtual Mat ApplyEffectToRegion(Mat src, Mat mask)
    {
        using var effect = ApplyEffect(src);
        return src.BlendByMask(effect, mask);
    }

    /// <summary>
    /// Gets the operation name to record in the edit history.
    /// </summary>
    protected abstract string OperationName { get; }

    public override Task ApplyAsync()
    {
        ApplyAndClose(_sourceImage is not null && _workingAlpha is not null ? BuildResult(_sourceImage.FullBgr) : null, OperationName);
        return Task.CompletedTask;
    }

    protected override void OnReset()
    {
        BrushRadius = 40;
        OnResetToolDefaults();
        WholeImage = false;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }

    /// <summary>Restores the tool's own parameter defaults. The common mask reset tail (brush
    /// radius, whole-image/paint flags, mask clearing and preview refresh) lives in the base.</summary>
    protected virtual void OnResetToolDefaults()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        _paintedMask?.Dispose();
    }
}
