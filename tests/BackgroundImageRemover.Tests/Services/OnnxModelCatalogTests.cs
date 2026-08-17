using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;

namespace BackgroundImageRemover.Tests.Services;

public class OnnxModelCatalogTests
{
    [Theory]
    [InlineData(OnnxModelKind.U2NetP)]
    [InlineData(OnnxModelKind.U2Net)]
    [InlineData(OnnxModelKind.Silueta)]
    [InlineData(OnnxModelKind.Rmbg14)]
    public void Get_ReturnsADefinitionMatchingTheRequestedKind(OnnxModelKind kind)
    {
        var def = OnnxModelCatalog.Get(kind);

        Assert.Equal(kind, def.Kind);
        Assert.False(string.IsNullOrWhiteSpace(def.FileName));
        Assert.False(string.IsNullOrWhiteSpace(def.DownloadUrl));
        Assert.False(string.IsNullOrWhiteSpace(def.DisplayName));
        Assert.True(def.InputSize > 0);
        Assert.Equal(3, def.Mean.Length);
        Assert.Equal(3, def.Std.Length);
    }

    [Fact]
    public void All_ContainsEveryModelKindExactlyOnce()
    {
        var kinds = OnnxModelCatalog.All.Select(d => d.Kind).ToList();

        Assert.Equal(Enum.GetValues<OnnxModelKind>().Length, kinds.Count);
        Assert.Equal(kinds.Distinct().Count(), kinds.Count);
    }

    [Fact]
    public void Get_UsesEnvironmentOverride_WhenSet()
    {
        const string envKey = "BIR_U2NETP_URL";
        string? previous = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "https://example.test/override.onnx");

            var def = OnnxModelCatalog.Get(OnnxModelKind.U2NetP);

            Assert.Equal("https://example.test/override.onnx", def.DownloadUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, previous);
        }
    }

    [Fact]
    public void Get_WithoutEnvironmentOverride_ReturnsTheBuiltInUrl()
    {
        const string envKey = "BIR_SILUETA_URL";
        string? previous = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, null);

            var def = OnnxModelCatalog.Get(OnnxModelKind.Silueta);

            Assert.Contains("silueta.onnx", def.DownloadUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, previous);
        }
    }
}
