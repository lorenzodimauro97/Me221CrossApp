using ME221CrossApp.Models;
using System.Runtime.CompilerServices;

namespace ME221CrossApp.Services;

public interface IEcuInteractionService
{
    Task<EcuInfo?> GetEcuInfoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EcuObjectDefinition>> GetObjectListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EcuObjectDefinition>> GetDataLinkListAsync(bool keepStreamActive = false, CancellationToken cancellationToken = default);
    Task<RealtimeDataPoint?> GetRealtimeDataValueAsync(ushort dataLinkId, CancellationToken cancellationToken = default);
    Task<TableData?> GetTableAsync(ushort tableId, CancellationToken cancellationToken = default);
    Task<DriverData?> GetDriverAsync(ushort driverId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IReadOnlyList<RealtimeDataPoint>> StreamRealtimeDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default);
    Task UpdateTableAsync(TableData table, CancellationToken cancellationToken = default);
    Task StoreTableAsync(ushort tableId, CancellationToken cancellationToken = default);
    Task UpdateDriverAsync(DriverData driver, CancellationToken cancellationToken = default);
    Task StoreDriverAsync(ushort driverId, CancellationToken cancellationToken = default);
}