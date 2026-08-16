using Microsoft.Win32;

namespace BackgroundImageRemover.Services.Dialogs;

public interface IDialogService
{
    string? ShowOpenImageDialog();
    string? ShowSavePngDialog(string? suggestedFileName);
    string? ShowOpenFolderDialog(string title);
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

    public string? ShowOpenFolderDialog(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
