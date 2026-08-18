using BackgroundImageRemover.ViewModels;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.ViewModels;

public class DocumentEditHistoryTests
{
    [Fact]
    public void RecordAndUndo_RestoresBgrAndAlpha()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(10, 20, 30));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Crop", bgr, alpha);

        bgr.SetTo(new Scalar(200, 200, 200));
        alpha.SetTo(new Scalar(0));

        Assert.True(history.Undo(ref bgr, ref alpha, out var name));
        Assert.Equal("Crop", name);

        var px = bgr!.At<Vec3b>(0, 0);
        Assert.Equal(10, px.Item0);
        Assert.Equal(20, px.Item1);
        Assert.Equal(30, px.Item2);
        Assert.Equal(255, alpha!.At<byte>(0, 0));

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void UndoThenRedo_RestoresMostRecentState()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(10, 10, 10));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Filters", bgr, alpha);
        bgr.SetTo(new Scalar(20, 20, 20));
        alpha.SetTo(new Scalar(128));

        Assert.True(history.Undo(ref bgr, ref alpha, out _));
        Assert.Equal(10, bgr!.At<Vec3b>(0, 0).Item0);

        Assert.True(history.Redo(ref bgr, ref alpha, out var name));
        Assert.Equal("Filters", name);
        Assert.Equal(20, bgr!.At<Vec3b>(0, 0).Item0);
        Assert.Equal(128, alpha!.At<byte>(0, 0));

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void BuildSteps_ReturnsTimelineInOrder()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(0, 0, 0));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Crop", bgr, alpha);
        bgr.SetTo(new Scalar(1, 1, 1));
        history.Record("Filters", bgr, alpha);
        bgr.SetTo(new Scalar(2, 2, 2));
        history.Record("Frame", bgr, alpha);
        bgr.SetTo(new Scalar(3, 3, 3));

        Assert.True(history.Undo(ref bgr, ref alpha, out _));

        var steps = history.BuildSteps();
        Assert.Equal(3, steps.Count);
        Assert.Equal("Crop", steps[0].Name);
        Assert.False(steps[0].IsUndone);
        Assert.Equal("Filters", steps[1].Name);
        Assert.False(steps[1].IsUndone);
        Assert.Equal("Frame", steps[2].Name);
        Assert.True(steps[2].IsUndone);

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void Record_ClearsRedoStack()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(10, 10, 10));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Edit", bgr, alpha);
        bgr.SetTo(new Scalar(20, 20, 20));
        Assert.True(history.Undo(ref bgr, ref alpha, out _));
        Assert.True(history.CanRedo);

        history.Record("Edit", bgr!, alpha!);
        Assert.False(history.CanRedo);

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void RestoreTo_UndoesBackToOlderStep()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(0, 0, 0));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Crop", bgr, alpha);
        bgr.SetTo(new Scalar(1, 1, 1));
        history.Record("Filters", bgr, alpha);
        bgr.SetTo(new Scalar(2, 2, 2));
        history.Record("Frame", bgr, alpha);
        bgr.SetTo(new Scalar(3, 3, 3));

        // Step 0 is "Crop" (oldest): jumping there undoes the two newer steps.
        Assert.True(history.RestoreTo(0, ref bgr, ref alpha, out _));
        Assert.Equal(0, bgr!.At<Vec3b>(0, 0).Item0);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void RestoreTo_RedoesForwardToUndoneStep()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(0, 0, 0));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Crop", bgr, alpha);
        bgr.SetTo(new Scalar(1, 1, 1));
        history.Record("Filters", bgr, alpha);
        bgr.SetTo(new Scalar(2, 2, 2));
        history.Record("Frame", bgr, alpha);
        bgr.SetTo(new Scalar(3, 3, 3));

        // Undo twice: the stack holds pre-operation states, so the current state is now
        // "Filters" (value 1), with "Frame" (value 2) waiting in the redo stack.
        Assert.True(history.Undo(ref bgr, ref alpha, out _));
        Assert.True(history.Undo(ref bgr, ref alpha, out _));
        Assert.Equal(1, bgr!.At<Vec3b>(0, 0).Item0);

        // Step 2 is "Frame" (latest): jumping there redoes forward to it.
        Assert.True(history.RestoreTo(2, ref bgr, ref alpha, out _));
        Assert.Equal(3, bgr!.At<Vec3b>(0, 0).Item0);
        Assert.False(history.CanRedo);

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void RestoreTo_MiddleStep_MakesItCurrent()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(0, 0, 0));
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1, new Scalar(255));

        history.Record("Crop", bgr, alpha);
        bgr.SetTo(new Scalar(1, 1, 1));
        history.Record("Filters", bgr, alpha);
        bgr.SetTo(new Scalar(2, 2, 2));
        history.Record("Frame", bgr, alpha);
        bgr.SetTo(new Scalar(3, 3, 3));

        // Undo once, then jump to the middle step "Filters" (index 1).
        Assert.True(history.Undo(ref bgr, ref alpha, out _));
        Assert.True(history.RestoreTo(1, ref bgr, ref alpha, out _));
        Assert.Equal(1, bgr!.At<Vec3b>(0, 0).Item0);
        Assert.True(history.CanRedo); // "Frame" is still ahead
        Assert.True(history.CanUndo); // "Crop" is behind

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void RestoreTo_OutOfRange_ReturnsFalse()
    {
        using var history = new DocumentEditHistory();
        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3);
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1);

        Assert.False(history.RestoreTo(-1, ref bgr, ref alpha, out _));
        Assert.False(history.RestoreTo(0, ref bgr, ref alpha, out _)); // empty history

        bgr?.Dispose();
        alpha?.Dispose();
    }

    [Fact]
    public void EmptyHistory_CannotUndoOrRedo()
    {
        using var history = new DocumentEditHistory();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Empty(history.BuildSteps());

        Mat? bgr = new Mat(2, 2, MatType.CV_8UC3);
        Mat? alpha = new Mat(2, 2, MatType.CV_8UC1);
        Assert.False(history.Undo(ref bgr, ref alpha, out _));
        Assert.False(history.Redo(ref bgr, ref alpha, out _));

        bgr?.Dispose();
        alpha?.Dispose();
    }
}
