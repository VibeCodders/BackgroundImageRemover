using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for selective and whole-image sharpening.</summary>
public partial class SharpenToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "🔪 Sharpen";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private double _strength = 0.5;

    protected override string OperationName => "Sharpen";

    public SharpenToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Choose whole-image or paint a region to sharpen, then apply.";
    }

    partial void OnStrengthChanged(double value) => RequestRefresh();

    protected override Mat ApplyEffect(Mat src) => SharpenService.SharpenAll(src, Strength);

    protected override void OnResetToolDefaults()
    {
        Strength = 0.5;
    }
}
