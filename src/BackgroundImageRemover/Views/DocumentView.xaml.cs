using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.Views.Controls;

namespace BackgroundImageRemover.Views;

/// <summary>
/// The per-tab editing surface: image panes, strategy panel, toolbar. Its DataContext is a
/// <see cref="DocumentViewModel"/>, supplied by the TabControl's ContentTemplate in MainWindow.
/// </summary>
public partial class DocumentView : UserControl
{
    private DocumentViewModel? ViewModel => DataContext as DocumentViewModel;

    private (int Width, int Height)? _lastDisplaySize;

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
    }

    private void DocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.PreviewBitmap) or nameof(DocumentViewModel.DisplayBitmap))
        {
            // Only reset the zoom/pan when the displayed image actually changes size (new
            // image loaded, or preview -> full-resolution result). Brush strokes recomposite
            // the result bitmap on every stamp without changing its dimensions, so blindly
            // resetting here would yank the view back to fit while the user is painting.
            var size = ViewModel?.DisplayBitmap is { } bitmap
                ? (bitmap.PixelWidth, bitmap.PixelHeight)
                : (0, 0);
            if (_lastDisplaySize == size)
            {
                return;
            }

            _lastDisplaySize = size;
            SinglePreview.ResetView();
            OriginalPreview.ResetView();
            ResultEditPreview.ResetView();
        }
    }

    private void DocumentView_DragOver(object sender, DragEventArgs e)
    {
        ViewInteractionHelper.HandleImageDragOver(e);
    }

    private async void DocumentView_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not null && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && ViewInteractionHelper.IsSupportedImage(files[0]))
        {
            await ViewModel.LoadAsync(files[0]);
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
    private void OriginalPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalWandClicked(e);

    private void ResultEditPreview_StrokeStart(object? sender, Point e)
    {
        if (ViewModel is null) return;
        ViewModel.OnResultStrokeStart(e, BrushPixelRadius(sender, ViewModel.BrushRadius));
    }

    private void ResultEditPreview_StrokeMove(object? sender, Point e)
    {
        if (ViewModel is null) return;
        ViewModel.OnResultStrokeMove(e, BrushPixelRadius(sender, ViewModel.BrushRadius));
    }

    private void ResultEditPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnResultStrokeEnd();
    private void ResultEditPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnResultWandClicked(e);

    private static double BrushPixelRadius(object? sender, double fallback)
        => sender is ImagePreviewControl preview ? preview.BrushRadius * preview.ImagePixelScale : fallback;
}
