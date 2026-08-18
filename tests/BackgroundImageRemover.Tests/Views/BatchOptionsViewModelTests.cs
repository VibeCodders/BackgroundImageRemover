using System.Windows.Media;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Views;

namespace BackgroundImageRemover.Tests.Views;

public class BatchOptionsViewModelTests
{
    [Fact]
    public void BuildOptions_Png_KeepsTransparencyAndSkipFlag()
    {
        var vm = new BatchOptionsViewModel { OutputKind = BatchOutputKind.Png, SkipExisting = true };

        var opts = vm.BuildOptions();

        Assert.False(opts.ExportJpeg);
        Assert.True(opts.SkipExisting);
    }

    [Fact]
    public void BuildOptions_Webp_KeepsTransparencyAndSkipFlag()
    {
        var vm = new BatchOptionsViewModel { OutputKind = BatchOutputKind.Webp, JpegQuality = 85, SkipExisting = true };

        var opts = vm.BuildOptions();

        Assert.False(opts.ExportJpeg);
        Assert.True(opts.ExportWebp);
        Assert.Equal(85, opts.JpegQuality); // reused as the WebP quality
        Assert.True(opts.SkipExisting);
    }

    [Fact]
    public void BuildOptions_JpegSolid_UsesPickedColor()
    {
        var vm = new BatchOptionsViewModel
        {
            OutputKind = BatchOutputKind.JpegSolid,
            SolidColor = Color.FromRgb(10, 20, 30),
            JpegQuality = 77,
            SkipExisting = true
        };

        var opts = vm.BuildOptions();

        Assert.True(opts.ExportJpeg);
        Assert.Equal(ExportBackgroundMode.SolidColor, opts.BackgroundMode);
        Assert.Equal(Color.FromRgb(10, 20, 30), opts.SolidColor);
        Assert.Equal(77, opts.JpegQuality);
        Assert.True(opts.SkipExisting);
    }

    [Fact]
    public void BuildOptions_JpegWhite_AlwaysUsesWhiteRegardlessOfPickedColor()
    {
        var vm = new BatchOptionsViewModel
        {
            OutputKind = BatchOutputKind.JpegWhite,
            SolidColor = Color.FromRgb(10, 20, 30)
        };

        var opts = vm.BuildOptions();

        Assert.True(opts.ExportJpeg);
        Assert.Equal(Colors.White, opts.SolidColor);
    }

    [Fact]
    public void BuildOptions_JpegGradient_UsesTopAndBottomColors()
    {
        var vm = new BatchOptionsViewModel
        {
            OutputKind = BatchOutputKind.JpegGradient,
            GradientTop = Color.FromRgb(1, 2, 3),
            GradientBottom = Color.FromRgb(4, 5, 6)
        };

        var opts = vm.BuildOptions();

        Assert.Equal(ExportBackgroundMode.Gradient, opts.BackgroundMode);
        Assert.Equal(Color.FromRgb(1, 2, 3), opts.GradientTop);
        Assert.Equal(Color.FromRgb(4, 5, 6), opts.GradientBottom);
    }

    [Fact]
    public void Restore_AppliesLastSessionFormatAndQuality()
    {
        var settings = new AppSettings
        {
            LastBatchOutputKind = nameof(BatchOutputKind.JpegBlur),
            LastBatchJpegQuality = 88,
            LastBatchSkipExisting = true
        };
        var vm = new BatchOptionsViewModel();

        vm.Restore(settings);

        Assert.Equal(BatchOutputKind.JpegBlur, vm.OutputKind);
        Assert.Equal(88, vm.JpegQuality);
        Assert.True(vm.SkipExisting);
    }

    [Fact]
    public void Restore_IgnoresUnknownKindAndOutOfRangeQuality()
    {
        var settings = new AppSettings
        {
            LastBatchOutputKind = "NotAKind",
            LastBatchJpegQuality = 5
        };
        var vm = new BatchOptionsViewModel { JpegQuality = 60 };

        vm.Restore(settings);

        Assert.Equal(BatchOutputKind.Png, vm.OutputKind); // default untouched
        Assert.Equal(60, vm.JpegQuality); // unchanged: 5 is below the valid slider range
    }

    [Fact]
    public void Persist_WritesChoicesToSettings()
    {
        var settings = new AppSettings();
        var vm = new BatchOptionsViewModel
        {
            OutputKind = BatchOutputKind.JpegSolid,
            JpegQuality = 92,
            SkipExisting = true
        };

        vm.Persist(settings);

        Assert.Equal(nameof(BatchOutputKind.JpegSolid), settings.LastBatchOutputKind);
        Assert.Equal(92, settings.LastBatchJpegQuality);
        Assert.True(settings.LastBatchSkipExisting);
    }
}
