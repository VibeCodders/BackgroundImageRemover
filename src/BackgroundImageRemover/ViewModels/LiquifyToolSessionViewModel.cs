using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for local warps (pinch, bloat, twirl, push).</summary>
public partial class LiquifyToolSessionViewModel : BgraToolSessionViewModelBase
{
    public override string ToolBadge => "✋ Liquify";
    public override string AccentColor => "#9333EA";

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

    public LiquifyToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitWorkingBgra();
        CenterX = WorkingBgra!.Width / 2;
        CenterY = WorkingBgra.Height / 2;
        RefreshResult();
        StatusMessage = "Choose a warp, set center/radius/strength, then apply.";
    }

    [RelayCommand]
    private void ApplyWarp()
    {
        if (WorkingBgra is null) return;
        ReplaceWorkingBgra(LiquifyService.Warp(WorkingBgra, new Point(CenterX, CenterY), Radius, Strength, Mode));
        IsDirty = true;
        RefreshResult();
    }

    protected override void OnReset()
    {
        if (_sourceImage is null) return;
        ResetWorkingBgra();
        CenterX = WorkingBgra.Width / 2;
        CenterY = WorkingBgra.Height / 2;
        Radius = 60;
        Strength = 0.5;
        Mode = LiquifyMode.Pinch;
        IsDirty = false;
        RefreshResult();
    }

    private void RefreshResult() => RefreshBgraPreview();

    public override Task ApplyAsync() => ApplyWorkingBgraAsync("Liquify");
}
