using OpenCvSharp;

namespace BackgroundImageRemover.Models;

/// <summary>Full-resolution source image (BGR, no alpha) plus its origin path.</summary>
public sealed class LoadedImage : IDisposable
{
    public string FilePath { get; }
    public Mat FullBgr { get; }

    /// <summary>
    /// Alpha channel of the source file when it had one (e.g. a previously exported cutout
    /// PNG). Null for ordinary photos without transparency.
    /// </summary>
    public Mat? FullAlpha { get; }

    public LoadedImage(string filePath, Mat fullBgr, Mat? fullAlpha = null)
    {
        FilePath = filePath;
        FullBgr = fullBgr;
        FullAlpha = fullAlpha;
    }

    /// <summary>Deep-clones the underlying Mats, so the copy can be handed to an independent
    /// owner (e.g. the standalone Uncrop window) without it mutating or disposing this instance's data.</summary>
    public LoadedImage Clone() => new(FilePath, FullBgr.Clone(), FullAlpha?.Clone());

    public void Dispose()
    {
        FullBgr.Dispose();
        FullAlpha?.Dispose();
    }
}
