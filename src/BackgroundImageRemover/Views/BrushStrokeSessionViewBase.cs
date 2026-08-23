using System;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>
/// Base for tool-session views whose preview supports freehand strokes (brush painting, lasso,
/// pen). Provides the standard StrokeStart/StrokeMove/StrokeEnd forwarding to the view model via
/// <see cref="IBrushStrokeSession"/>, so derived views only wire the shared handlers in XAML.
/// </summary>
public abstract class BrushStrokeSessionViewBase : UserControl
{
    private IBrushStrokeSession? ViewModel => DataContext as IBrushStrokeSession;

    public void OnStrokeStart(object? sender, Point e)
        => ViewModel?.OnStrokeStart(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    public void OnStrokeMove(object? sender, Point e)
        => ViewModel?.OnStrokeMove(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    public void OnStrokeEnd(object? sender, EventArgs e)
        => ViewModel?.OnStrokeEnd();
}
