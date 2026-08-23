using BackgroundImageRemover.Services.Onnx;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Pins the LaMa tensor contract (independent of the ~200 MB model): BGR→RGB/255 image fill,
/// 0/255→0/1 mask fill, and the 0-255 CHW output→BGR readback.
/// </summary>
public class LamaInpaintEngineTests
{
    [Fact]
    public void FillImageTensor_ConvertsBgrToRgbScaledToUnit()
    {
        const int size = 8;
        using var bgr = new Mat(size, size, MatType.CV_8UC3, new Scalar(10, 20, 30)); // B,G,R
        var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });
        int plane = size * size;

        LamaInpaintEngine.FillImageTensor(bgr, tensor);

        // R = 30/255, G = 20/255, B = 10/255 for every pixel.
        Assert.Equal(30f / 255f, tensor[0, 0, 3, 4], 5);
        Assert.Equal(20f / 255f, tensor[0, 1, 3, 4], 5);
        Assert.Equal(10f / 255f, tensor[0, 2, 3, 4], 5);
    }

    [Fact]
    public void FillMaskTensor_BinarizesTheMask()
    {
        const int size = 8;
        using var mask = new Mat(size, size, MatType.CV_8UC1, Scalar.All(0));
        mask.Set(2, 3, (byte)255);
        mask.Set(5, 6, (byte)128);
        var tensor = new DenseTensor<float>(new[] { 1, 1, size, size });

        LamaInpaintEngine.FillMaskTensor(mask, tensor);

        Assert.Equal(1f, tensor[0, 0, 2, 3]);
        Assert.Equal(1f, tensor[0, 0, 5, 6]); // any nonzero byte is "masked"
        Assert.Equal(0f, tensor[0, 0, 0, 0]);
    }

    [Fact]
    public void TensorToBgr_ReadsChwUnitRangeIntoBgrBytes()
    {
        const int size = 4;
        var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });
        int plane = size * size;
        // R=200, G=100, B=50 at (1,2); a clamped 300 elsewhere in R.
        tensor[0, 0, 1, 2] = 200f;
        tensor[0, 1, 1, 2] = 100f;
        tensor[0, 2, 1, 2] = 50f;
        tensor[0, 0, 0, 0] = 300f;

        using var bgr = LamaInpaintEngine.TensorToBgr(tensor);

        var px = bgr.At<Vec3b>(1, 2);
        Assert.Equal(50, px.Item0);  // B
        Assert.Equal(100, px.Item1); // G
        Assert.Equal(200, px.Item2); // R
        // Clamped to 255.
        Assert.Equal(255, bgr.At<Vec3b>(0, 0).Item2);
    }

    [Fact]
    public void TensorToBgr_RoundTripsThroughFillImageTensor()
    {
        const int size = 16;
        using var src = new Mat(size, size, MatType.CV_8UC3, new Scalar(7, 33, 250));
        var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });
        LamaInpaintEngine.FillImageTensor(src, tensor);

        // The model output is 0-255, i.e. the normalized input scaled back by 255.
        var mem = tensor.Buffer;
        for (int i = 0; i < mem.Length; i++)
        {
            mem.Span[i] *= 255f;
        }
        using var back = LamaInpaintEngine.TensorToBgr(tensor);

        var px = back.At<Vec3b>(9, 9);
        Assert.Equal(7, px.Item0);
        Assert.Equal(33, px.Item1);
        Assert.Equal(250, px.Item2);
    }
}
