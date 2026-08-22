using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for cropping (rectangle, aspect presets, margins, auto-trim, straighten).</summary>
public partial class CropToolSessionViewModel : BgraToolSessionViewModelBase
{
    public override string ToolBadge => "✂ Crop";
    public override string AccentColor => "#16A34A";

    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;

    [ObservableProperty]
    private UncropAspectPreset _selectedAspect = UncropAspectPresets.Free;

    [ObservableProperty]
    private double _angle = 0.0;

    [ObservableProperty]
    private int _marginLeft;

    [ObservableProperty]
    private int _marginTop;

    [ObservableProperty]
    private int _marginRight;

    [ObservableProperty]
    private int _marginBottom;

    [ObservableProperty]
    private Rect? _selectedRect;

    [ObservableProperty]
    private int _targetWidth;

    [ObservableProperty]
    private int _targetHeight;

    public CropToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitWorkingBgra();
        SelectedRect = new Rect(0, 0, WorkingBgra!.Width, WorkingBgra.Height);
        RefreshResult();
        StatusMessage = "Drag a rectangle on the left, or use the presets and margins.";
    }

    partial void OnSelectedAspectChanged(UncropAspectPreset value)
    {
        if (WorkingBgra is null) return;
        if (value.Ratio is not { } ratio)
        {
            return;
        }
        SelectedRect = CropService.CenteredRectForAspect(WorkingBgra.Size(), ratio);
        RefreshResult();
    }

    partial void OnAngleChanged(double value) => RefreshResult();

    public void OnRectSelected(Rect rect)
    {
        SelectedRect = rect;
        RefreshResult();
    }

    [RelayCommand]
    private void AutoTrim()
    {
        if (WorkingBgra is null) return;
        SelectedRect = CropService.TrimContentBounds(WorkingBgra);
        RefreshResult();
    }

    [RelayCommand]
    private void TrimWhite() => TrimToColor(new Vec3b(255, 255, 255));

    [RelayCommand]
    private void TrimBlack() => TrimToColor(new Vec3b(0, 0, 0));

    [RelayCommand]
    private void AutoStraighten()
    {
        if (WorkingBgra is null) return;
        double skew = TransformService.EstimateSkewAngle(WorkingBgra);
        Angle = Math.Clamp(-skew, -45, 45);
        StatusMessage = Math.Abs(skew) < 0.05 ? "No skew detected." : $"Detected {skew:0.0}° skew.";
    }

    [RelayCommand]
    private void ApplySize()
    {
        if (WorkingBgra is null) return;
        int width = Math.Clamp(TargetWidth, 1, WorkingBgra.Width);
        int height = Math.Clamp(TargetHeight, 1, WorkingBgra.Height);
        SelectedRect = CropService.CenteredRectForSize(WorkingBgra.Size(), width, height);
        RefreshResult();
    }

    [RelayCommand]
    private void Rotate90Clockwise() => Rotate90(clockwise: true);

    [RelayCommand]
    private void Rotate90CounterClockwise() => Rotate90(clockwise: false);

    private void Rotate90(bool clockwise)
    {
        if (WorkingBgra is null) return;
        var rotated = clockwise
            ? TransformService.Rotate90Clockwise(WorkingBgra)
            : TransformService.Rotate90CounterClockwise(WorkingBgra);
        WorkingBgra.Dispose();
        WorkingBgra = rotated;
        SourceBitmap = WorkingBgra.ToBitmapSource();
        SelectedAspect = UncropAspectPresets.Free;
        Angle = 0.0;
        SelectedRect = new Rect(0, 0, WorkingBgra.Width, WorkingBgra.Height);
        RefreshResult();
    }

    private void TrimToColor(Vec3b color)
    {
        if (WorkingBgra is null) return;
        SelectedRect = CropService.TrimContentBounds(WorkingBgra, color, 12);
        RefreshResult();
    }

    [RelayCommand]
    private void ApplyMargins()
    {
        if (WorkingBgra is null) return;
        int width = Math.Max(1, WorkingBgra.Width - MarginLeft - MarginRight);
        int height = Math.Max(1, WorkingBgra.Height - MarginTop - MarginBottom);
        SelectedRect = new Rect(
            Math.Clamp(MarginLeft, 0, WorkingBgra.Width - 1),
            Math.Clamp(MarginTop, 0, WorkingBgra.Height - 1),
            width, height);
        RefreshResult();
    }

    [RelayCommand]
    private void Reset()
    {
        if (WorkingBgra is null) return;
        SelectedAspect = UncropAspectPresets.Free;
        Angle = 0.0;
        MarginLeft = MarginTop = MarginRight = MarginBottom = 0;
        TargetWidth = TargetHeight = 0;
        SelectedRect = new Rect(0, 0, WorkingBgra.Width, WorkingBgra.Height);
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (WorkingBgra is null) return;
        using var cropped = CropService.CropRect(WorkingBgra, SelectedRect ?? new Rect(0, 0, WorkingBgra.Width, WorkingBgra.Height));
        using var rotated = Math.Abs(Angle) > 1e-6 ? TransformService.Rotate(cropped, Angle) : cropped.Clone();
        ResultBitmap = rotated.ToBitmapSource();
        IsDirty = true;
    }

    public override Task ApplyAsync()
    {
        if (WorkingBgra is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        using var cropped = CropService.CropRect(WorkingBgra, SelectedRect ?? new Rect(0, 0, WorkingBgra.Width, WorkingBgra.Height));
        using var rotated = Math.Abs(Angle) > 1e-6 ? TransformService.Rotate(cropped, Angle) : cropped.Clone();
        ApplyBgraResult(rotated, "Crop");
        return Task.CompletedTask;
    }
}
