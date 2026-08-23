using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Verifies the RotateToolSessionViewModel default state, live preview refresh and the
/// Apply flow that pushes the rotated result back into the parent document.
/// </summary>
public class RotateToolSessionViewModelTests
{
    // The loader produces a 6-wide × 4-tall image so 90° rotation visibly swaps the dimensions.
    private const int Width = 6;
    private const int Height = 4;

    [Fact]
    public async Task DefaultState_HasZeroAngleAndExpandOn()
    {
        var (doc, shell) = await CreateDocumentAsync();

        var vm = new RotateToolSessionViewModel(shell, doc);

        Assert.Equal(0.0, vm.Angle);
        Assert.True(vm.Expand);
        Assert.NotNull(vm.ResultBitmap); // preview refreshed in ctor
        Assert.False(vm.IsDirty); // zero angle -> no pending edit
    }

    [Fact]
    public async Task ApplyCommand_WithAngle_PushesRotatedResultToParent()
    {
        var (doc, shell) = await CreateDocumentAsync();

        var vm = new RotateToolSessionViewModel(shell, doc);

        vm.Angle = 90;
        vm.ApplyCommand.Execute(null);

        // The parent document must now hold a rotated working result (90° swaps dimensions).
        Assert.True(doc.HasWorkingResult);
        Assert.Equal(Height, doc.ImageWidth);  // original Height -> new Width
        Assert.Equal(Width, doc.ImageHeight); // original Width -> new Height
        Assert.Contains(doc.EditSteps, s => s.Name == "Rotate" && !s.IsUndone);
    }

    [Fact]
    public async Task ResetCommand_ClearsAngleAndRestoresPreview()
    {
        var (doc, shell) = await CreateDocumentAsync();

        var vm = new RotateToolSessionViewModel(shell, doc);

        vm.Angle = 45;
        vm.Expand = false;
        Assert.True(vm.IsDirty);

        vm.ResetCommand.Execute(null);

        Assert.Equal(0.0, vm.Angle);
        Assert.True(vm.Expand);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task ChangingAngle_RefreshsPreview()
    {
        var (doc, shell) = await CreateDocumentAsync();

        var vm = new RotateToolSessionViewModel(shell, doc);

        vm.Angle = 90;

        Assert.NotNull(vm.ResultBitmap);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task ApplyCommand_AtZeroAngle_DoesNotRecordEdit()
    {
        var (doc, shell) = await CreateDocumentAsync();

        var vm = new RotateToolSessionViewModel(shell, doc);

        vm.ApplyCommand.Execute(null);

        Assert.False(doc.HasWorkingResult);
        Assert.Empty(doc.EditSteps);
    }

    [Fact]
    public async Task Dispose_DoesNotThrow()
    {
        var (doc, shell) = await CreateDocumentAsync();

        var vm = new RotateToolSessionViewModel(shell, doc);

        vm.Dispose(); // should not throw
    }

    private static async Task<(DocumentViewModel Doc, FakeShell Shell)> CreateDocumentAsync()
    {
        var (doc, shell) = TestDoubles.CreateDocumentAndShell(Width, Height);
        await doc.LoadImageAsync("photo.jpg");
        return (doc, shell);
    }
}
