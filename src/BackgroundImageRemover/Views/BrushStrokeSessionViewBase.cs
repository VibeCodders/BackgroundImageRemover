using System;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>
/// Base for tool-session views whose preview supports painting strokes over a mask. Provides the
/// standard StrokeStart/StrokeMove/StrokeEnd forwarding to the view model's
/// <c>OnBrushStrokeStart/Move/End</c>, so derived views only wire the shared handlers in XAML.
/// </summary>
public abstract class BrushStrokeSessionViewBase : UserControl
{
    private MaskToolSessionViewModelBase? ViewModel => DataContext as MaskToolSessionViewModelBase;

    public void OnStrokeStart(object? sender, Point e)
        => ViewModel?.OnBrushStrokeStart(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    public void OnStrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    public void OnStrokeEnd(object? sender, EventArgs e)
        => ViewModel?.OnBrushStrokeEnd();
}
