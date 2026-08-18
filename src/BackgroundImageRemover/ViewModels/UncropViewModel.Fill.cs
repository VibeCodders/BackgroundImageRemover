using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class UncropViewModel
{
    /// <summary>Seeds the window with an image handed in from the main window (a clone, so this
    /// window's own lifecycle/EditHistory never touches the source document's Mats).</summary>
    public void LoadInitialImage(LoadedImage image) => AdoptImage(image);

    public async Task LoadAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading image...";
            var image = await _imageLoader.LoadAsync(path);
            Title = Path.GetFileName(path) + " (Uncrop)";
            AdoptImage(image);
            StatusMessage = $"Loaded {Path.GetFileName(path)} ({image.FullBgr.Width}x{image.FullBgr.Height})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
            _log.Error("Uncrop: could not load image", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOpenImage() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenImage))]
    private async Task OpenImageAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }

        await LoadAsync(path);
    }

    private void AdoptImage(LoadedImage image)
    {
        _sourceImage?.Dispose();
        _resultSession.Clear();
        IsDirty = false;
        RefreshUndoRedoState();
        SaveAsCommand.NotifyCanExecuteChanged();

        _sourceImage = image;
        if (string.IsNullOrEmpty(Title) || Title == "Uncrop")
        {
            Title = !string.IsNullOrEmpty(image.FilePath) ? Path.GetFileName(image.FilePath) + " (Uncrop)" : "Uncrop";
        }
        SourceBitmap = image.FullBgr.ToBitmapSource();
        PreviewResult = null;
        IsImageLoaded = true;
        Options.Reset();
    }

    private bool CanApplyFill() => IsImageLoaded && !IsBusy && Options.CanExecute();

    private bool CanCancelFill() => IsBusy && _fillCts is not null && !_fillCts.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanCancelFill))]
    private void CancelFill()
    {
        if (_fillCts is not null && !_fillCts.IsCancellationRequested)
        {
            _fillCts.Cancel();
            StatusMessage = "Cancelling fill operation...";
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyFill))]
    private async Task ApplyFillAsync()
    {
        if (_sourceImage is null)
        {
            return;
        }

        var config = Options.ToConfig();

        _fillCts?.Dispose();
        _fillCts = new CancellationTokenSource();
        var ct = _fillCts.Token;

        try
        {
            IsBusy = true;
            CancelFillCommand.NotifyCanExecuteChanged();
            StatusMessage = "Filling...";

            using var filledBgr = await UncropOperationHelper.ExecuteUncropAsync(
                _sourceImage.FullBgr, config, _fillService, ct);

            var bgra = UncropOperationHelper.ApplyFinishing(filledBgr, config);

            _resultSession.Replace(bgra);

            RefreshUndoRedoState();
            SaveAsCommand.NotifyCanExecuteChanged();
            PreviewResult = _resultSession.Result!.ToBitmapSource();
            IsDirty = true;
            StatusMessage = $"Applied {config.FillMode} fill.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Fill operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fill failed: {ex.Message}";
            _log.Error("Uncrop: fill failed", ex);
        }
        finally
        {
            _fillCts?.Dispose();
            _fillCts = null;
            IsBusy = false;
            CancelFillCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanUndoExecute() => _resultSession.CanUndo;
    private bool CanRedoExecute() => _resultSession.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (!_resultSession.Undo())
        {
            return;
        }
        PreviewResult = _resultSession.Result!.ToBitmapSource();
        IsDirty = true;
        RefreshUndoRedoState();
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (!_resultSession.Redo())
        {
            return;
        }
        PreviewResult = _resultSession.Result!.ToBitmapSource();
        IsDirty = true;
        RefreshUndoRedoState();
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() => _resultSession.HasResult && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsAsync()
    {
        await _resultSession.SaveAsync();
    }

    public void Dispose()
    {
        _fillCts?.Cancel();
        _fillCts?.Dispose();
        _sourceImage?.Dispose();
        _resultSession.Dispose();
    }
}
