using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class BackgroundRemoverToolSessionView : UserControl
{
    private BackgroundRemoverToolSessionViewModel? ViewModel => DataContext as BackgroundRemoverToolSessionViewModel;

    public BackgroundRemoverToolSessionView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Number keys (1-8, no modifiers) switch the removal strategy while the session has
    /// focus. Keys typed into text-entry controls are ignored so the shortcuts never fight
    /// the user's input.
    /// </summary>
    private void Root_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (e.OriginalSource is TextBoxBase or ComboBox or ComboBoxItem)
        {
            return;
        }

        if (ViewModel is not null && StrategyShortcuts.StrategyForKey(e.Key) is { } strategy)
        {
            ViewModel.SelectedStrategy = strategy;
            e.Handled = true;
        }
    }

    private void OriginalPreview_RectSelected(object? sender, OpenCvSharp.Rect e)
    {
        if (ViewModel is null) return;
        ViewModel.GrabCut.SelectedRect = e;

        // After the first rectangle is drawn, switch to EditRect so the user can
        // move/resize the rect with corner handles instead of redrawing.
        if (ViewModel.OriginalMode == InteractionMode.DrawRect)
        {
            ViewModel.OriginalMode = InteractionMode.EditRect;
            OriginalPreview.SetEditRect(e.X, e.Y, e.Width, e.Height);
        }
    }

    private void OriginalPreview_EditRectSelected(object? sender, OpenCvSharp.Rect e)
    {
        if (ViewModel is not null) ViewModel.GrabCut.SelectedRect = e;
    }

    private void OriginalPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnOriginalStrokeStart(e);
    private void OriginalPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnOriginalStrokeMove(e);
    private void OriginalPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnOriginalStrokeEnd();
    private void OriginalPreview_SamPointClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalSamPointClicked(e);
    private void OriginalPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalWandClicked(e);

    private void ChooseSolidColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ParentDocument is { } doc) doc.IsColorPickerOpen = !doc.IsColorPickerOpen;
    }

    private void ChooseGradientTopColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ParentDocument is { } doc) doc.IsGradientTopColorPickerOpen = !doc.IsGradientTopColorPickerOpen;
    }

    private void ChooseGradientBottomColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ParentDocument is { } doc) doc.IsGradientBottomColorPickerOpen = !doc.IsGradientBottomColorPickerOpen;
    }
}
