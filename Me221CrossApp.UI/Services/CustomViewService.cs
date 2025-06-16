using System.Text.Json;
using ME221CrossApp.Models;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.UI.Services;

public class CustomViewService(ILogger<CustomViewService> logger) : ICustomViewService
{
    private readonly string _storePath = Path.Combine(FileSystem.AppDataDirectory, "custom_views.json");
    private List<CustomViewDefinition> _views = [];
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public event Action? OnCustomViewsChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            logger.LogInformation("Initializing CustomViewService and loading from {StorePath}", _storePath);
            if (!File.Exists(_storePath))
            {
                _views = [];
                _isInitialized = true;
                return;
            }

            try
            {
                await using var storeStream = new FileStream(_storePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var views = await JsonSerializer.DeserializeAsync<List<CustomViewDefinition>>(storeStream, cancellationToken: cancellationToken);
                _views = views ?? [];
                logger.LogInformation("Loaded {Count} custom views.", _views.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load custom views from {StorePath}", _storePath);
                _views = [];
            }
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task SaveToStoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            await using var storeStream = new FileStream(_storePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(storeStream, _views, options, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save custom views to {StorePath}", _storePath);
        }
    }

    public async Task<IReadOnlyList<CustomViewDefinition>> GetCustomViewsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return _views;
    }

    public async Task<CustomViewDefinition?> GetCustomViewByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return _views.FirstOrDefault(v => v.Id == id);
    }

    public async Task SaveCustomViewAsync(CustomViewDefinition view, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var existingIndex = _views.FindIndex(v => v.Id == view.Id);
        if (existingIndex >= 0)
        {
            _views[existingIndex] = view;
        }
        else
        {
            _views.Add(view);
        }
        await SaveToStoreAsync(cancellationToken);
        OnCustomViewsChanged?.Invoke();
    }

    public async Task DeleteCustomViewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var removed = _views.RemoveAll(v => v.Id == id);
        if (removed > 0)
        {
            await SaveToStoreAsync(cancellationToken);
            OnCustomViewsChanged?.Invoke();
        }
    }
}