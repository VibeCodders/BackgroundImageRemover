using System;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

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
        => ViewModel?.OnBrushStrokeStart(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void BlurPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void BlurPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnBrushStrokeEnd();
}
