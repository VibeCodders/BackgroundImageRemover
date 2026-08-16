using Microsoft.Win32;

namespace BackgroundImageRemover.Services.Dialogs;

public interface IDialogService
{
    string? ShowOpenImageDialog();
    string? ShowSavePngDialog(string? suggestedFileName);
}

public sealed class DialogService : IDialogService
{
    public string? ShowOpenImageDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSavePngDialog(string? suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export PNG",
            Filter = "PNG image|*.png",
            FileName = suggestedFileName ?? "cutout.png",
            DefaultExt = ".png"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
