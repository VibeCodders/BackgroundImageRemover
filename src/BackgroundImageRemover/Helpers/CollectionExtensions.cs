namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Utility helpers for operating on collections and bounded stacks.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Trims the stack from the bottom (oldest entries) so that at most <paramref name="maxDepth"/> entries remain.
    /// Elements being dropped are passed to <paramref name="onDrop"/> (e.g. for disposing resources).
    /// </summary>
    public static void TrimStack<T>(this Stack<T> stack, int maxDepth, Action<T>? onDrop = null)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth cannot be negative.");
        }

        while (stack.Count > maxDepth)
        {
            // Stack.ToArray() returns elements from top (newest, index 0) to bottom (oldest, index ^1).
            var items = stack.ToArray();
            stack.Clear();
            for (int i = maxDepth - 1; i >= 0; i--)
            {
                stack.Push(items[i]);
            }

            for (int i = maxDepth; i < items.Length; i++)
            {
                onDrop?.Invoke(items[i]);
            }
        }
    }
}
