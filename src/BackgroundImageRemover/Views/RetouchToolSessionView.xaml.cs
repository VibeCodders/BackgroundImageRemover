using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class RetouchToolSessionView : BrushStrokeSessionViewBase
{
    private RetouchToolSessionViewModel? ViewModel => DataContext as RetouchToolSessionViewModel;

    public RetouchToolSessionView()
    {
        InitializeComponent();
    }

    private void ResultEditPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnResultWandClicked(e);
}
