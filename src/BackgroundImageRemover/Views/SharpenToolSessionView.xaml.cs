using System;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.Views.Controls;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="SharpenToolSessionView"/>.</summary>
public partial class SharpenToolSessionView : UserControl
{
    private SharpenToolSessionViewModel? ViewModel => DataContext as SharpenToolSessionViewModel;

    public SharpenToolSessionView()
    {
        InitializeComponent();
    }

    private void SharpenPreview_StrokeStart(object? sender, Point e)
        => ViewModel?.OnBrushStrokeStart(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void SharpenPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void SharpenPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnBrushStrokeEnd();

    private static double BrushPixelRadius(object? sender, double fallback)
        => sender is ImagePreviewControl preview ? preview.BrushRadius * preview.ImagePixelScale : fallback;
}
