using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

public partial class DodgeBurnToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "☀ Dodge / Burn";
    public override string AccentColor => "#B45309";

    [ObservableProperty]
    [ToolParameter]
    private bool _dodge = true;

    [ObservableProperty]
    [ToolParameter]
    private double _strength = 0.3;

    protected override string OperationName => "DodgeBurn";

    public DodgeBurnToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Dodge (lighten) or Burn (darken) a region, then apply.";
    }

    protected override Mat ApplyEffect(Mat src) => DodgeBurnService.DodgeBurnAll(src, Dodge, Strength);

    protected override void OnResetToolDefaults()
    {
        Dodge = true;
        Strength = 0.3;
    }
}
