using Microsoft.Win32;

namespace BackgroundImageRemover.Services.Dialogs;

public interface IDialogService
{
    string? ShowOpenImageDialog();
    string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG");
    string? ShowOpenFolderDialog(string title);
    string? ShowOpenProjectDialog();
    string? ShowSaveProjectDialog(string? suggestedFileName);
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

    public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
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

    public string? ShowOpenProjectDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Project",
            Filter = "BackgroundImageRemover project|*.ibrproj|All files|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveProjectDialog(string? suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Project",
            Filter = "BackgroundImageRemover project|*.ibrproj",
            FileName = suggestedFileName ?? "project.ibrproj",
            DefaultExt = ".ibrproj"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
