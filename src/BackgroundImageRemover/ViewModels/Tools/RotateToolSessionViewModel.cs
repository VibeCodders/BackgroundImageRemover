using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>
/// Dedicated tool tab for arbitrary-angle rotation of the current document. The rotation
/// center is the image center; when <see cref="Expand"/> is enabled the canvas grows to fit
/// the rotated result, otherwise the original dimensions are kept (corners clipped to
/// transparent). The applied rotation is pushed back into the parent document as an undoable
/// edit.
/// </summary>
public partial class RotateToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;
    private Mat? _workingBgra;

    public override string ToolBadge => "↺ Rotate";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private double _angle;

    [ObservableProperty]
    private bool _expand = true;

    [ObservableProperty]
    private string? _statusMessage;

    public RotateToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _workingBgra = _sourceImage.FullBgr.ToBgra(_workingAlpha);
        SourceBitmap = _workingBgra.ToBitmapSource();
        RefreshPreview();
        StatusMessage = "Rotate by an arbitrary angle. Expand keeps the full image; un-tick to keep the original canvas size.";
    }

    partial void OnAngleChanged(double value) => RefreshPreview();
    partial void OnExpandChanged(bool value) => RefreshPreview();

    private void RefreshPreview()
    {
        if (_workingBgra is null) return;

        using var rotated = RotateService.Rotate(_workingBgra, Angle, Expand);
        ResultBitmap = rotated.ToBitmapSource();
        IsDirty = Math.Abs(Angle % 360) > 1e-6;
    }

    [RelayCommand]
    private void Apply()
    {
        if (_workingBgra is null) return;

        // A zero angle is a no-op: don't push a redundant edit onto the undo stack.
        if (Math.Abs(Angle % 360) < 1e-6)
        {
            _shell.CloseTabDirect(this);
            return;
        }

        using var rotated = RotateService.Rotate(_workingBgra, Angle, Expand);
        var (bgr, alpha) = BackgroundCompositingService.SplitBgra(rotated);
        _parentDocument.ApplyToolResult(bgr, alpha, "Rotate");

        _shell.CloseTabDirect(this);
    }

    [RelayCommand]
    private void Reset()
    {
        Angle = 0.0;
        Expand = true;
        RefreshPreview();
    }

    public override Task ApplyAsync()
    {
        Apply();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
        _workingBgra?.Dispose();
    }
}
