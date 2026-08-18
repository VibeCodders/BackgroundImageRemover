using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Owns the uncrop result Mat and its <see cref="EditHistory"/>, and performs the shared
/// undo/redo/save-as plumbing used by both the standalone Uncrop window and the Uncrop tool tab.
/// The hosting ViewModel supplies small callbacks so this class stays free of ViewModel types.
/// </summary>
public sealed class UncropResultSession : IDisposable
{
    private readonly EditHistory _editHistory = new();
    private readonly Func<LoadedImage?> _sourceImageProvider;
    private readonly IDialogService _dialogs;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;
    private readonly Action<bool> _setBusy;
    private readonly Action<bool> _setDirty;
    private readonly Action<string?> _setStatusMessage;
    private Mat? _resultBgra;

    public UncropResultSession(
        Func<LoadedImage?> sourceImageProvider,
        IDialogService dialogs,
        IImageExportService imageExporter,
        IFileLogService log,
        Action<bool> setBusy,
        Action<bool> setDirty,
        Action<string?> setStatusMessage)
    {
        _sourceImageProvider = sourceImageProvider;
        _dialogs = dialogs;
        _imageExporter = imageExporter;
        _log = log;
        _setBusy = setBusy;
        _setDirty = setDirty;
        _setStatusMessage = setStatusMessage;
    }

    public bool CanUndo => _editHistory.CanUndo;
    public bool CanRedo => _editHistory.CanRedo;
    public Mat? Result => _resultBgra;
    public bool HasResult => _resultBgra is not null;

    /// <summary>Replaces the current result, pushing the previous one onto the undo stack.</summary>
    public void Replace(Mat newResult)
    {
        if (_resultBgra is not null)
        {
            _editHistory.Push(_resultBgra);
            _resultBgra.Dispose();
        }
        _resultBgra = newResult;
    }

    /// <summary>Undoes to the previous result. Returns false when there is nothing to undo.</summary>
    public bool Undo()
    {
        if (_resultBgra is null)
        {
            return false;
        }
        var restored = _editHistory.Undo(_resultBgra);
        if (restored is null)
        {
            return false;
        }
        _resultBgra.Dispose();
        _resultBgra = restored;
        return true;
    }

    /// <summary>Redoes the previously undone result. Returns false when there is nothing to redo.</summary>
    public bool Redo()
    {
        if (_resultBgra is null)
        {
            return false;
        }
        var restored = _editHistory.Redo(_resultBgra);
        if (restored is null)
        {
            return false;
        }
        _resultBgra.Dispose();
        _resultBgra = restored;
        return true;
    }

    /// <summary>Drops the current result and clears the history (e.g. when adopting a new image).</summary>
    public void Clear()
    {
        _resultBgra?.Dispose();
        _resultBgra = null;
        _editHistory.Clear();
    }

    /// <summary>Exports the current result through the save dialog. No-ops when there is no result.</summary>
    public async Task SaveAsync()
    {
        if (_resultBgra is null)
        {
            return;
        }

        var sourceImage = _sourceImageProvider();
        var baseName = sourceImage is not null ? Path.GetFileNameWithoutExtension(sourceImage.FilePath) : "uncrop";
        var path = _dialogs.ShowSavePngDialog(baseName + "_uncrop.png", "Export Uncropped Image");
        if (path is null)
        {
            return;
        }

        try
        {
            _setBusy(true);
            await _imageExporter.ExportPngAsync(_resultBgra, path);
            _setDirty(false);
            _setStatusMessage($"Exported to {path}");
            _log.Info($"Uncrop exported to {path}");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Export failed: {ex.Message}");
            _log.Error("Uncrop: export failed", ex);
        }
        finally
        {
            _setBusy(false);
        }
    }

    public void Dispose()
    {
        _resultBgra?.Dispose();
        _editHistory.Dispose();
    }
}
