using BackgroundImageRemover.Services.Onnx;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class LamaModelFilesTests
{
    [Fact]
    public void All_ContainsThreeVariants_WithDefaultLast()
    {
        Assert.Equal(3, LamaModelFiles.All.Count);
        Assert.Equal(LamaModelVariant.Small, LamaModelFiles.All[0].Variant);
        Assert.Equal(LamaModelVariant.Middle, LamaModelFiles.All[1].Variant);
        Assert.Equal(LamaModelVariant.Large, LamaModelFiles.All[2].Variant);
        Assert.True(LamaModelFiles.All[2].ApproxSizeMb > LamaModelFiles.All[0].ApproxSizeMb);
    }

    [Fact]
    public void FileName_MapsPerVariant()
    {
        Assert.Equal("lama_small.onnx", LamaModelFiles.FileName(LamaModelVariant.Small));
        Assert.Equal("lama_middle.onnx", LamaModelFiles.FileName(LamaModelVariant.Middle));
        Assert.Equal("lama_fp32.onnx", LamaModelFiles.FileName(LamaModelVariant.Large));
    }

    [Fact]
    public void Url_DefaultsToVerifiedCarveModel_ForLarge()
    {
        Assert.StartsWith("https://huggingface.co/Carve/LaMa-ONNX", LamaModelFiles.Url(LamaModelVariant.Large));
    }

    [Fact]
    public void Url_HonorsEnvironmentOverrides()
    {
        const string smallUrl = "https://example.invalid/lama_small.onnx";
        const string middleUrl = "https://example.invalid/lama_middle.onnx";
        try
        {
            Environment.SetEnvironmentVariable("BIR_LAMA_SMALL_URL", smallUrl);
            Environment.SetEnvironmentVariable("BIR_LAMA_MIDDLE_URL", middleUrl);

            Assert.Equal(smallUrl, LamaModelFiles.Url(LamaModelVariant.Small));
            Assert.Equal(middleUrl, LamaModelFiles.Url(LamaModelVariant.Middle));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BIR_LAMA_SMALL_URL", null);
            Environment.SetEnvironmentVariable("BIR_LAMA_MIDDLE_URL", null);
        }
    }

    [Fact]
    public void Option_ReturnsDefault_ForUnknownVariant()
    {
        Assert.Equal(LamaModelVariant.Large, LamaModelFiles.Option((LamaModelVariant)999).Variant);
    }
}
