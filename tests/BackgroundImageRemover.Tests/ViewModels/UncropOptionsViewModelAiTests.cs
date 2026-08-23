using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.ViewModels;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

public class UncropOptionsViewModelAiTests
{
    [Fact]
    public void Defaults_ToBigLama_WithoutGpu()
    {
        var vm = new UncropOptionsViewModel();

        Assert.Equal(LamaModelVariant.Large, vm.SelectedAiModel.Variant);
        Assert.False(vm.UseGpu);
        Assert.Equal(3, vm.AiModels.Count);
    }

    [Fact]
    public void ToConfig_CarriesSelectedModelVariantAndGpuPreference()
    {
        var vm = new UncropOptionsViewModel();
        vm.SelectedAiModel = LamaModelFiles.All[0]; // Small
        vm.UseGpu = true;

        var config = vm.ToConfig();

        Assert.Equal(LamaModelVariant.Small, config.AiModelVariant);
        Assert.True(config.UseGpu);
    }

    [Fact]
    public void ToConfig_Defaults_ToLargeAndCpu()
    {
        var vm = new UncropOptionsViewModel();

        var config = vm.ToConfig();

        Assert.Equal(LamaModelVariant.Large, config.AiModelVariant);
        Assert.False(config.UseGpu);
    }
}
