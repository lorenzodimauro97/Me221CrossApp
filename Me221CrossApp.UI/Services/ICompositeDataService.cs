using ME221CrossApp.Models;
using System.Runtime.CompilerServices;

namespace Me221CrossApp.UI.Services;

public interface ICompositeDataService
{
    Task<IReadOnlyList<EcuObjectDefinition>> GetAvailableDataLinksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EcuObjectDefinition>> GetAllEcuObjectDefinitionsAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<IReadOnlyList<RealtimeDataPoint>> StreamCompositeDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default);
}