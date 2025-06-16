using ME221CrossApp.Models;

namespace ME221CrossApp.UI.Services;

public interface ICustomViewService
{
    event Action? OnCustomViewsChanged;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomViewDefinition>> GetCustomViewsAsync(CancellationToken cancellationToken = default);
    Task<CustomViewDefinition?> GetCustomViewByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveCustomViewAsync(CustomViewDefinition view, CancellationToken cancellationToken = default);
    Task DeleteCustomViewAsync(Guid id, CancellationToken cancellationToken = default);
}