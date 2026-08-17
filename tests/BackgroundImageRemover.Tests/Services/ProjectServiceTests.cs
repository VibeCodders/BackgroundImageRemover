using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Projects;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class ProjectServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrips_OriginalAndWorkingImages()
    {
        // Original: opaque photo (no alpha).
        using var originalBgr = new Mat(40, 30, MatType.CV_8UC3, new Scalar(10, 20, 30));
        originalBgr.At<Vec3b>(0, 0) = new Vec3b(1, 2, 3);

        // Working result: composited BGR + alpha (cutout).
        using var workingBgr = new Mat(40, 30, MatType.CV_8UC3, new Scalar(50, 60, 70));
        workingBgr.At<Vec3b>(5, 5) = new Vec3b(200, 100, 50);
        using var workingAlpha = new Mat(40, 30, MatType.CV_8UC1, Scalar.All(255));
        workingAlpha.At<byte>(5, 5) = 0;

        var settings = new ProjectDocument
        {
            SelectedStrategy = nameof(StrategyKind.ChromaKey),
            ChromaKeyTolerance = 42,
            BrushRadius = 7,
            MagicWandTolerance = 9,
            GrabCutRect = new[] { 3, 4, 50, 60 },
            SamPoint = new[] { 11, 22 }
        };

        var path = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}.ibrproj");
        try
        {
            var service = new ProjectService();
            await service.SaveAsync(path, originalBgr, null, workingBgr, workingAlpha, settings);

            using var loaded = await service.LoadAsync(path);

            Assert.Null(loaded.OriginalAlpha);
            Assert.NotNull(loaded.WorkingBgr);
            Assert.NotNull(loaded.WorkingAlpha);

            var originalPx = loaded.OriginalBgr.At<Vec3b>(0, 0);
            Assert.Equal(1, originalPx.Item0);
            Assert.Equal(2, originalPx.Item1);
            Assert.Equal(3, originalPx.Item2);

            var workingPx = loaded.WorkingBgr!.At<Vec3b>(5, 5);
            Assert.Equal(200, workingPx.Item0);
            Assert.Equal(100, workingPx.Item1);
            Assert.Equal(50, workingPx.Item2);

            Assert.Equal(0, loaded.WorkingAlpha!.At<byte>(5, 5));
            Assert.Equal(255, loaded.WorkingAlpha.At<byte>(0, 0));

            Assert.Equal(42, loaded.Settings.ChromaKeyTolerance);
            Assert.Equal(7, loaded.Settings.BrushRadius);
            Assert.Equal(9, loaded.Settings.MagicWandTolerance);
            Assert.Equal(new[] { 3, 4, 50, 60 }, loaded.Settings.GrabCutRect);
            Assert.Equal(new[] { 11, 22 }, loaded.Settings.SamPoint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsNewerFormatVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}.ibrproj");
        try
        {
            File.WriteAllText(path, "{\"Version\":999,\"OriginalImagePng\":null,\"WorkingImagePng\":null}");

            var service = new ProjectService();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync(path));
            Assert.Contains("newer version", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesOriginalAlpha_ForCutoutSource()
    {
        // Original cutout: BGR with black under transparency + alpha mask.
        using var originalBgr = new Mat(20, 20, MatType.CV_8UC3, Scalar.All(0));
        originalBgr.At<Vec3b>(10, 10) = new Vec3b(1, 2, 3);
        using var originalAlpha = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(0));
        originalAlpha.At<byte>(10, 10) = 255;

        var settings = new ProjectDocument();

        var path = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}.ibrproj");
        try
        {
            var service = new ProjectService();
            await service.SaveAsync(path, originalBgr, originalAlpha, null, null, settings);

            using var loaded = await service.LoadAsync(path);

            Assert.NotNull(loaded.OriginalAlpha);
            Assert.Null(loaded.WorkingBgr);
            Assert.Null(loaded.WorkingAlpha);

            Assert.Equal(255, loaded.OriginalAlpha!.At<byte>(10, 10));
            Assert.Equal(0, loaded.OriginalAlpha.At<byte>(0, 0));

            var originalPx = loaded.OriginalBgr.At<Vec3b>(10, 10);
            Assert.Equal(1, originalPx.Item0);
            Assert.Equal(2, originalPx.Item1);
            Assert.Equal(3, originalPx.Item2);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
