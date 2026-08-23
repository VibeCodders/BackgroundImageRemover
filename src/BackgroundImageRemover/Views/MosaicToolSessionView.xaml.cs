using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class MosaicToolSessionView : BrushStrokeSessionViewBase
{
    private MosaicToolSessionViewModel? ViewModel => DataContext as MosaicToolSessionViewModel;

    public MosaicToolSessionView()
    {
        InitializeComponent();
    }

    private void MosaicPreview_RectSelected(object? sender, OpenCvSharp.Rect e) => ViewModel?.OnRectSelected(e);
}
