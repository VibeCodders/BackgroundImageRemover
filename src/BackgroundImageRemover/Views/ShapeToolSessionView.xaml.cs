using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="ShapeToolSessionView"/>.</summary>
public partial class ShapeToolSessionView : UserControl
{
    private ShapeToolSessionViewModel? ViewModel => DataContext as ShapeToolSessionViewModel;

    public ShapeToolSessionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ShapePreview.CursorImagePositionChanged += OnCursorImagePositionChanged;
        ShapePreview.RotationSelected += OnRotationSelected;
    }

    private void OnRotationSelected(object? sender, double degrees)
    {
        if (ViewModel is { } vm)
        {
            vm.Rotation = degrees;
        }
    }

    private void OnCursorImagePositionChanged(object? sender, System.Windows.Point? position)
    {
        CursorPositionLabel.Text = position is { } p ? $"x: {(int)p.X}  y: {(int)p.Y}" : string.Empty;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ShapeToolSessionViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is ShapeToolSessionViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            PushEditRectFromVm(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ShapeToolSessionViewModel.PositionX):
            case nameof(ShapeToolSessionViewModel.PositionY):
            case nameof(ShapeToolSessionViewModel.SizeWidth):
            case nameof(ShapeToolSessionViewModel.SizeHeight):
                PushEditRectFromVm(vm);
                break;
            case nameof(ShapeToolSessionViewModel.Rotation):
                ShapePreview.SetEditRotation(vm.Rotation);
                break;
        }
    }

    /// <summary>Keeps the edit handles on the preview in sync when the shape is changed via the
    /// percentage sliders (no event loop: SetEditRect never raises back to the view model).</summary>
    private void PushEditRectFromVm(ShapeToolSessionViewModel vm)
    {
        if (ShapePreview.ImageSource is not { } image)
        {
            return;
        }

        int w = image.PixelWidth;
        int h = image.PixelHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        ShapePreview.SetEditRect(
            (int)Math.Round(vm.PositionX / 100.0 * w),
            (int)Math.Round(vm.PositionY / 100.0 * h),
            (int)Math.Round(vm.SizeWidth / 100.0 * w),
            (int)Math.Round(vm.SizeHeight / 100.0 * h));
    }

    private void ChooseStrokeColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsStrokeColorPickerOpen = !vm.IsStrokeColorPickerOpen;
    }

    private void ChooseFillColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsFillColorPickerOpen = !vm.IsFillColorPickerOpen;
    }

    private void ShapePreview_RectSelected(object? sender, OpenCvSharp.Rect rect)
    {
        ViewModel?.OnRectSelected(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
