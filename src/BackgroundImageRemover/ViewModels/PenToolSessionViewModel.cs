using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using OcvPoint = OpenCvSharp.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for freehand drawing (pen/brush): the user drags on the image to draw
/// strokes in a chosen color and width. Strokes accumulate until Apply bakes them into the document.
/// </summary>
public partial class PenToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "✏️ Pen";
    public override string AccentColor => "#0EA5E9";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    /// <summary>Pen width in display units (DIPs); the preview cursor matches it exactly.</summary>
    [ObservableProperty]
    private double _penWidth = 6;

    [ObservableProperty]
    private WpfColor _color = WpfColor.FromRgb(30, 30, 30);

    [ObservableProperty]
    private bool _isColorPickerOpen;

    private readonly List<PenStroke> _strokes = new();
    private PenStroke? _current;

    public PenToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha);
        }
        StatusMessage = "Drag on the image to draw freehand. Change color/width any time.";
        RefreshPenPreview();
    }

    partial void OnPenWidthChanged(double value) => RefreshPenPreview();
    partial void OnColorChanged(WpfColor value) => RefreshPenPreview();

    public void OnStrokeStart(WpfPoint imagePoint, double radiusPx)
    {
        _current = new PenStroke(new List<OcvPoint> { ToOcv(imagePoint) }, ToRadius(radiusPx));
        RefreshPenPreview();
    }

    public void OnStrokeMove(WpfPoint imagePoint, double radiusPx)
    {
        if (_current is null)
        {
            return;
        }
        _current.Points.Add(ToOcv(imagePoint));
        RefreshPenPreview();
    }

    public void OnStrokeEnd()
    {
        if (_current is { } stroke)
        {
            _strokes.Add(stroke);
            _current = null;
        }
        RefreshPenPreview();
    }

    [RelayCommand]
    private void Clear()
    {
        _strokes.Clear();
        _current = null;
        RefreshPenPreview();
    }

    private void RefreshPenPreview()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            return;
        }

        using var work = CloneWorkingBgr();
        var toRender = new List<PenStroke>(_strokes);
        if (_current is not null)
        {
            toRender.Add(_current);
        }
        using var rendered = PenRenderService.Draw(work, toRender, Color.ToVec3b());
        ResultBitmap = rendered.ToBitmapSource(_workingAlpha);

        IsDirty = _strokes.Count > 0 || _current is not null;
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            using var work = CloneWorkingBgr();
            using var rendered = PenRenderService.Draw(work, _strokes, Color.ToVec3b());
            _parentDocument.ApplyToolResult(rendered, _workingAlpha.Clone(), "Pen");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    private static OcvPoint ToOcv(WpfPoint p) => new((int)Math.Round(p.X), (int)Math.Round(p.Y));

    private static int ToRadius(double radiusPx) => Math.Max(1, (int)Math.Round(radiusPx));
}
