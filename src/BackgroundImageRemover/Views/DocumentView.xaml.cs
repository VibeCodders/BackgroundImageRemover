using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>
/// The per-tab editing surface: image panes, strategy panel, toolbar. Its DataContext is a
/// <see cref="DocumentViewModel"/>, supplied by the TabControl's ContentTemplate in MainWindow.
/// </summary>
public partial class DocumentView : UserControl
{
    private DocumentViewModel? ViewModel => DataContext as DocumentViewModel;

    public DocumentView()
    {
        InitializeComponent();
        Loaded += DocumentView_Loaded;
        Unloaded += DocumentView_Unloaded;
    }

    private void DocumentView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.ScribbleStrokeUndone += ViewModel_ScribbleStrokeUndone;
        ViewModel.ScribbleStrokeRedone += ViewModel_ScribbleStrokeRedone;
        ViewModel.ScribblesCleared += ViewModel_ScribblesCleared;
    }

    private void DocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.ScribbleStrokeUndone -= ViewModel_ScribbleStrokeUndone;
        ViewModel.ScribbleStrokeRedone -= ViewModel_ScribbleStrokeRedone;
        ViewModel.ScribblesCleared -= ViewModel_ScribblesCleared;
    }

    private void ViewModel_ScribbleStrokeUndone(object? sender, EventArgs e) => OriginalPreview.UndoScribbleStroke();
    private void ViewModel_ScribbleStrokeRedone(object? sender, EventArgs e) => OriginalPreview.RedoScribbleStroke();
    private void ViewModel_ScribblesCleared(object? sender, EventArgs e) => OriginalPreview.ClearScribbleStrokes();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.PreviewBitmap))
        {
            OriginalPreview.ResetView();
            ResultEditPreview.ResetView();
        }
    }

    private void DocumentView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DocumentView_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not null && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await ViewModel.LoadImageAsync(files[0]);
        }
    }

    private void OriginalPreview_RectSelected(object? sender, OpenCvSharp.Rect e)
    {
        if (ViewModel is not null) ViewModel.GrabCut.SelectedRect = e;
    }

    private void OriginalPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnOriginalStrokeStart(e);
    private void OriginalPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnOriginalStrokeMove(e);
    private void OriginalPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnOriginalStrokeEnd();
    private void OriginalPreview_SamPointClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalSamPointClicked(e);

    private void ResultEditPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnResultStrokeStart(e);
    private void ResultEditPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnResultStrokeMove(e);
    private void ResultEditPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnResultStrokeEnd();
    private void ResultEditPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnResultWandClicked(e);

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.IsColorPickerOpen = !ViewModel.IsColorPickerOpen;
    }
}
