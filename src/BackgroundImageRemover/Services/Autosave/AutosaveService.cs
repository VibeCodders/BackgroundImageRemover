using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Services.Autosave;

/// <summary>A single crash-recovery snapshot recorded in the autosave manifest.</summary>
public sealed record RecoveryEntry
{
    public string Id { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTime SavedAt { get; init; }
}

public interface IAutosaveService
{
    /// <summary>Starts tracking the shell's open documents and the periodic timer.</summary>
    void Start(ShellViewModel shell);

    /// <summary>Stops tracking and the timer (used on exit).</summary>
    void Stop();

    /// <summary>Writes recovery snapshots for every currently dirty document and refreshes the manifest.</summary>
    Task RunAutosaveAsync();

    /// <summary>Recovery snapshots left behind by a previous (crashed) session.</summary>
    IReadOnlyList<RecoveryEntry> PendingRecovery { get; }

    bool HasPendingRecovery { get; }

    /// <summary>Deletes a specific recovery snapshot after it has been restored.</summary>
    void RemoveRecoveryEntry(string id);

    /// <summary>Deletes all recovery data (user declined the restore prompt).</summary>
    void DiscardAllRecovery();

    /// <summary>Removes the whole recovery folder on a clean exit.</summary>
    void CleanupOnExit();
}

/// <summary>
/// Periodically persists dirty documents into a recovery folder as full <c>.ibrproj</c>
/// snapshots, keeping a small manifest so the app can offer to restore them after a crash.
/// The recovery data is removed as soon as a document is saved or closed cleanly, and wiped
/// entirely on a normal exit, so leftover entries always mean \"the app did not shut down
/// cleanly\". Best-effort by design: failures are logged and never block the UI.
/// </summary>
public sealed class AutosaveService : IAutosaveService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISettingsService _settings;
    private readonly IProjectService _projectService;
    private readonly IFileLogService _log;
    private readonly string _recoveryDir;
    private readonly System.Threading.Timer? _timer;
    private readonly Dictionary<DocumentViewModel, string> _docFiles = new();
    private readonly object _gate = new();
    private ShellViewModel? _shell;
    private int _saving;

    public AutosaveService(ISettingsService settings, IProjectService projectService, IFileLogService log)
        : this(settings, projectService, log, null)
    {
    }

    /// <summary>Testable constructor: <paramref name="recoveryDirOverride"/> redirects the recovery folder.</summary>
    internal AutosaveService(
        ISettingsService settings,
        IProjectService projectService,
        IFileLogService log,
        string? recoveryDirOverride)
    {
        _settings = settings;
        _projectService = projectService;
        _log = log;
        _recoveryDir = recoveryDirOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BackgroundImageRemover", "recovery");

        if (settings.Current.EnableAutosave)
        {
            int minutes = Math.Max(1, settings.Current.AutosaveIntervalMinutes);
            _timer = new System.Threading.Timer(
                _ => Tick(), null, TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(minutes));
        }
    }

    private string ManifestPath => Path.Combine(_recoveryDir, "manifest.json");

    public void Start(ShellViewModel shell)
    {
        _shell = shell;
        shell.Documents.CollectionChanged += Documents_CollectionChanged;
        foreach (var tab in shell.Documents.OfType<DocumentViewModel>())
        {
            Track(tab);
        }
    }

    public void Stop()
    {
        if (_shell is not null)
        {
            _shell.Documents.CollectionChanged -= Documents_CollectionChanged;
            _shell = null;
        }
        foreach (var doc in _docFiles.Keys.ToList())
        {
            Untrack(doc);
        }
    }

    public async Task RunAutosaveAsync()
    {
        List<(DocumentViewModel Doc, string Path)> toSave;
        List<DocumentViewModel> toRemove;
        lock (_gate)
        {
            toSave = _docFiles
                .Where(kv => kv.Key.IsImageLoaded && kv.Key.IsDirty && !kv.Key.IsBusy)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
            toRemove = _docFiles.Keys
                .Where(d => !d.IsImageLoaded || !d.IsDirty)
                .ToList();
        }

        foreach (var doc in toRemove)
        {
            RemoveRecoveryFile(doc);
        }

        foreach (var (doc, path) in toSave)
        {
            try
            {
                Directory.CreateDirectory(_recoveryDir);
                await doc.SaveRecoverySnapshotAsync(path);
            }
            catch (Exception ex)
            {
                _log.Error($"Autosave failed for {doc.Title}", ex);
            }
        }

        RewriteManifest();
    }

    public IReadOnlyList<RecoveryEntry> PendingRecovery
    {
        get
        {
            try
            {
                if (!File.Exists(ManifestPath))
                {
                    return Array.Empty<RecoveryEntry>();
                }
                var entries = JsonSerializer.Deserialize<List<RecoveryEntry>>(File.ReadAllText(ManifestPath))
                    ?? new List<RecoveryEntry>();
                return entries.Where(e => File.Exists(e.FilePath)).ToList();
            }
            catch (Exception ex)
            {
                _log.Error("Could not read the recovery manifest", ex);
                return Array.Empty<RecoveryEntry>();
            }
        }
    }

    public bool HasPendingRecovery => PendingRecovery.Count > 0;

    public void RemoveRecoveryEntry(string id)
    {
        TryDelete(Path.Combine(_recoveryDir, id + ".ibrproj"));
        RewriteManifest();
    }

    public void DiscardAllRecovery()
    {
        try
        {
            if (Directory.Exists(_recoveryDir))
            {
                Directory.Delete(_recoveryDir, true);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Could not discard the recovery data", ex);
        }
    }

    public void CleanupOnExit() => DiscardAllRecovery();

    public void Dispose()
    {
        _timer?.Dispose();
        Stop();
    }

    private void Tick()
    {
        // Never overlap autosave passes.
        if (Interlocked.Exchange(ref _saving, 1) != 0)
        {
            return;
        }
        _ = RunAutosaveAsync().ContinueWith(
            _ => Interlocked.Exchange(ref _saving, 0), TaskScheduler.Default);
    }

    private void Documents_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var tab in e.NewItems.OfType<DocumentViewModel>())
            {
                Track(tab);
            }
        }
        if (e.OldItems is not null)
        {
            foreach (var tab in e.OldItems.OfType<DocumentViewModel>())
            {
                Untrack(tab);
            }
        }
    }

    private void Track(DocumentViewModel doc)
    {
        lock (_gate)
        {
            if (_docFiles.ContainsKey(doc))
            {
                return;
            }
            _docFiles[doc] = Path.Combine(_recoveryDir, Guid.NewGuid().ToString("N") + ".ibrproj");
        }
        doc.PropertyChanged += Doc_PropertyChanged;
    }

    private void Untrack(DocumentViewModel doc)
    {
        doc.PropertyChanged -= Doc_PropertyChanged;
        RemoveRecoveryFile(doc);
    }

    private void Doc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Once the document is no longer dirty (saved or discarded) its recovery snapshot is
        // obsolete and must not be offered again after the next launch.
        if (sender is DocumentViewModel { IsDirty: false } doc
            && e.PropertyName == nameof(DocumentViewModel.IsDirty))
        {
            RemoveRecoveryFile(doc);
        }
    }

    private void RemoveRecoveryFile(DocumentViewModel doc)
    {
        string? path;
        lock (_gate)
        {
            if (!_docFiles.Remove(doc, out path))
            {
                return;
            }
        }
        TryDelete(path);
        RewriteManifest();
    }

    private void RewriteManifest()
    {
        try
        {
            List<RecoveryEntry> entries;
            lock (_gate)
            {
                entries = _docFiles
                    .Where(kv => File.Exists(kv.Value))
                    .Select(kv => new RecoveryEntry
                    {
                        Id = Path.GetFileNameWithoutExtension(kv.Value),
                        FilePath = kv.Value,
                        Title = kv.Key.Title,
                        SavedAt = File.GetLastWriteTime(kv.Value)
                    })
                    .ToList();
            }

            if (entries.Count == 0)
            {
                TryDelete(ManifestPath);
                return;
            }

            Directory.CreateDirectory(_recoveryDir);
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.Error("Could not write the recovery manifest", ex);
        }
    }

    private void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _log.Error($"Could not delete recovery file {path}", ex);
        }
    }
}
