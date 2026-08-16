using OpenCvSharp;

namespace BackgroundImageRemover.Models;

/// <summary>Result of a background-removal computation: a BGRA cutout plus timing info.</summary>
public sealed class RemovalResult : IDisposable
{
    public Mat Bgra { get; }
    public double ElapsedMilliseconds { get; }

    public RemovalResult(Mat bgra, double elapsedMilliseconds)
    {
        Bgra = bgra;
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    public void Dispose() => Bgra.Dispose();
}
