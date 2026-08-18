using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class EditHistoryTests
{
    [Fact]
    public void NewHistory_CannotUndoOrRedo()
    {
        using var history = new EditHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Push_MakesUndoAvailable_ButNotRedo()
    {
        using var history = new EditHistory();
        using var state = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));

        history.Push(state);

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Undo_ReturnsThePushedState_AndEnablesRedo()
    {
        using var history = new EditHistory();
        using var original = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        history.Push(original);

        using var current = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        using var restored = history.Undo(current);

        Assert.NotNull(restored);
        Assert.Equal(255, restored!.At<byte>(0, 0));
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
    }

    [Fact]
    public void Undo_WithNothingPushed_ReturnsNull()
    {
        using var history = new EditHistory();
        using var current = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));

        Assert.Null(history.Undo(current));
    }

    [Fact]
    public void Redo_AfterUndo_RestoresTheStateThatWasUndone()
    {
        using var history = new EditHistory();
        using var original = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        history.Push(original);

        using var edited = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        using var afterUndo = history.Undo(edited)!;

        using var redone = history.Redo(afterUndo);

        Assert.NotNull(redone);
        Assert.Equal(0, redone!.At<byte>(0, 0));
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Push_ClearsTheRedoStack()
    {
        using var history = new EditHistory();
        using var first = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        history.Push(first);
        using var second = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        using var afterUndo = history.Undo(second)!;
        Assert.True(history.CanRedo);

        using var newState = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(128));
        history.Push(newState);

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Push_MutatingTheOriginalMatAfterward_DoesNotAffectTheSnapshot()
    {
        // Push clones its input, so the caller's live Mat can keep mutating in place.
        using var history = new EditHistory();
        using var state = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        history.Push(state);

        state.SetTo(Scalar.All(0));

        using var current = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        using var restored = history.Undo(current);

        Assert.Equal(255, restored!.At<byte>(0, 0));
    }

    [Fact]
    public void Clear_RemovesUndoAndRedoAvailability()
    {
        using var history = new EditHistory();
        using var first = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        history.Push(first);
        using var second = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        using var afterUndo = history.Undo(second)!;

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Push_ExceedingMaxDepth_DropsOldestAndPreservesOrder()
    {
        using var history = new EditHistory();

        // Push 25 states (MaxDepth is 20)
        for (int i = 1; i <= 25; i++)
        {
            using var mat = new Mat(1, 1, MatType.CV_8UC1, Scalar.All(i));
            history.Push(mat);
        }

        Assert.True(history.CanUndo);

        // Current state is 26; undo should return 25, 24, ..., down to 6 (20 entries)
        using var live = new Mat(1, 1, MatType.CV_8UC1, Scalar.All(26));
        for (int expected = 25; expected >= 6; expected--)
        {
            using var restored = history.Undo(live);
            Assert.NotNull(restored);
            Assert.Equal(expected, restored!.At<byte>(0, 0));
        }

        // After 20 undos, history should be exhausted
        Assert.False(history.CanUndo);
        Assert.Null(history.Undo(live));
    }
}
