using BackgroundImageRemover.Helpers;

namespace BackgroundImageRemover.Tests.Helpers;

public class CollectionExtensionsTests
{
    [Fact]
    public void TrimStack_ThrowsWhenMaxDepthIsNegative()
    {
        var stack = new Stack<int>();
        stack.Push(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.TrimStack(-1));
    }

    [Fact]
    public void TrimStack_DoesNothingWhenCountLessThanOrEqualToMaxDepth()
    {
        var stack = new Stack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        var dropped = new List<int>();
        stack.TrimStack(3, dropped.Add);

        Assert.Equal(3, stack.Count);
        Assert.Empty(dropped);
        Assert.Equal(3, stack.Pop());
        Assert.Equal(2, stack.Pop());
        Assert.Equal(1, stack.Pop());
    }

    [Fact]
    public void TrimStack_DropsOldestEntriesAndPreservesStackOrder()
    {
        var stack = new Stack<int>();
        // Push 1, 2, 3, 4, 5 (stack top is 5, bottom is 1)
        for (int i = 1; i <= 5; i++)
        {
            stack.Push(i);
        }

        var dropped = new List<int>();
        stack.TrimStack(3, dropped.Add);

        // Should keep the top 3 (5, 4, 3) and drop 2 and 1
        Assert.Equal(3, stack.Count);
        Assert.Equal(new[] { 2, 1 }, dropped);

        Assert.Equal(5, stack.Pop());
        Assert.Equal(4, stack.Pop());
        Assert.Equal(3, stack.Pop());
        Assert.Empty(stack);
    }

    [Fact]
    public void TrimStack_WithMaxDepthZero_ClearsStackAndDropsAll()
    {
        var stack = new Stack<string>();
        // Push "a", "b", "c" (stack top is "c", bottom is "a")
        stack.Push("a");
        stack.Push("b");
        stack.Push("c");

        var dropped = new List<string>();
        stack.TrimStack(0, dropped.Add);

        Assert.Empty(stack);
        Assert.Equal(new[] { "c", "b", "a" }, dropped);
    }
}
