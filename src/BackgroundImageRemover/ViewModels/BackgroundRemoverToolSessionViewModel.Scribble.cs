using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.Input;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class BackgroundRemoverToolSessionViewModel
{
    [RelayCommand]
    private void SetOriginalScribbleMode(InteractionMode mode)
        => OriginalMode = OriginalMode == mode ? InteractionMode.DrawRect : mode;

    public void OnOriginalStrokeStart(WpfPoint imagePoint)
    {
        if (_preview is null) return;
        ScribbleManager.EnsureMats(_preview.Bgr.Size());

        ScribbleManager.Start(imagePoint, OriginalMode);

        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        RefreshScribbleOverlay();
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        ScribbleManager.Move(imagePoint, OriginalMode);

        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        RefreshScribbleOverlay();
    }

    public void OnOriginalStrokeEnd()
    {
        ScribbleManager.EndStroke();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        RefreshScribbleOverlay();
    }

    private void RefreshScribbleOverlay()
    {
        ScribbleOverlay = ScribbleManager.BuildOverlayBitmap();
    }

    private bool CanUndoScribble => ScribbleManager.CanUndo;
    private bool CanRedoScribble => ScribbleManager.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoScribble))]
    public void UndoScribble()
    {
        ScribbleManager.Undo();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedoScribble))]
    public void RedoScribble()
    {
        ScribbleManager.Redo();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Clears all SAM prompt points (both primary and additional).</summary>
    [RelayCommand]
    private void ClearSamPoints()
    {
        ClearSamPromptPoints();
    }
}
