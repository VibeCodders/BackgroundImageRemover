using OpenCvSharp;

namespace BackgroundImageRemover.Models;

/// <summary>Full-resolution source image (BGR, no alpha) plus its origin path.</summary>
public sealed class LoadedImage : IDisposable
{
    public string FilePath { get; }
    public Mat FullBgr { get; }

    public LoadedImage(string filePath, Mat fullBgr)
    {
        FilePath = filePath;
        FullBgr = fullBgr;
    }

    public void Dispose() => FullBgr.Dispose();
}
