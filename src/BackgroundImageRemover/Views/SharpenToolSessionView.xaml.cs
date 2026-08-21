using System;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

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
        => ViewModel?.OnBrushStrokeStart(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void SharpenPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void SharpenPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnBrushStrokeEnd();
}
