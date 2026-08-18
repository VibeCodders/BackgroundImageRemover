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

        var scribbleMode = ScribbleManager.FromInteractionMode(OriginalMode);

        ScribbleManager.StartStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        var scribbleMode = ScribbleManager.FromInteractionMode(OriginalMode);

        ScribbleManager.MoveStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeEnd()
    {
        ScribbleManager.EndStroke();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
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
}
