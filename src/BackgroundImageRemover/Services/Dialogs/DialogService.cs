using System.Windows;
using BackgroundImageRemover.Models;
using Microsoft.Win32;

namespace BackgroundImageRemover.Services.Dialogs;

public enum CloseDocumentResult
{
    Save,
    Discard,
    Cancel
}

public interface IDialogService
{
    string? ShowOpenImageDialog();
    string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG");
    string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG");
    string? ShowOpenFolderDialog(string title);
    string? ShowOpenProjectDialog();
    string? ShowSaveProjectDialog(string? suggestedFileName);
    CloseDocumentResult ConfirmCloseDocument(string documentName);
}

public sealed class DialogService : IDialogService
{
    public string? ShowOpenImageDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.jfif;*.bmp;*.webp;*.gif;*.tif;*.tiff;*.ico|All files|*.*"
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

    public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = "JPEG image|*.jpg;*.jpeg",
            FileName = suggestedFileName ?? "cutout.jpg",
            DefaultExt = ".jpg"
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

    public CloseDocumentResult ConfirmCloseDocument(string documentName)
    {
        var result = MessageBox.Show(
            $"Save changes to \"{documentName}\" before closing?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);

        return result switch
        {
            MessageBoxResult.Yes => CloseDocumentResult.Save,
            MessageBoxResult.No => CloseDocumentResult.Discard,
            _ => CloseDocumentResult.Cancel
        };
    }
}
