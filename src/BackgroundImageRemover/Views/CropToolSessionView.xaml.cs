using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class CropToolSessionView : UserControl
{
    private CropToolSessionViewModel? ViewModel => DataContext as CropToolSessionViewModel;

    public CropToolSessionView()
    {
        InitializeComponent();
    }

    private void CropPreview_RectSelected(object? sender, OpenCvSharp.Rect e) => ViewModel?.OnRectSelected(e);
}
