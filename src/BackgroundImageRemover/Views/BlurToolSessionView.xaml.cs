using System;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.Views.Controls;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="BlurToolSessionView"/>.</summary>
public partial class BlurToolSessionView : UserControl
{
    private BlurToolSessionViewModel? ViewModel => DataContext as BlurToolSessionViewModel;

    public BlurToolSessionView()
    {
        InitializeComponent();
    }

    private void BlurPreview_StrokeStart(object? sender, Point e)
        => ViewModel?.OnBrushStrokeStart(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void BlurPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void BlurPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnBrushStrokeEnd();

    private static double BrushPixelRadius(object? sender, double fallback)
        => sender is ImagePreviewControl preview ? preview.BrushRadius * preview.ImagePixelScale : fallback;
}
