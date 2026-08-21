using System.Windows.Media;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

public sealed class ToolSessionViewModelUtilityTests
{
    [Fact]
    public void ToVec3b_ConvertsWpfColorCorrectly()
    {
        var color = Color.FromRgb(10, 20, 30);
        var vec3b = color.ToVec3b();

        Assert.Equal(30, vec3b.Item0); // B
        Assert.Equal(20, vec3b.Item1); // G
        Assert.Equal(10, vec3b.Item2); // R
    }

    [Fact]
    public void TrySetResultBitmap_ReturnsFalseWhenResultIsNull()
    {
        BitmapSource? bitmap;
        var result = ToolSessionViewModelUtility.TrySetResultBitmap(
            null!, null, null, out bitmap);

        Assert.False(result);
        Assert.Null(bitmap);
    }

    [Fact]
    public void IsDirtyFrom_ReturnsTrueWhenAnyConditionIsTrue()
    {
        Assert.True(ToolSessionViewModelUtility.IsDirtyFrom(true, false, false));
        Assert.True(ToolSessionViewModelUtility.IsDirtyFrom(false, true, false));
        Assert.False(ToolSessionViewModelUtility.IsDirtyFrom(false, false, false));
    }

    [Fact]
    public void IsEffectSignificant_ReturnsTrueForValuesAboveEpsilon()
    {
        Assert.True(ToolSessionViewModelUtility.IsEffectSignificant(0.0011));
        Assert.False(ToolSessionViewModelUtility.IsEffectSignificant(0.0001));
    }
}
