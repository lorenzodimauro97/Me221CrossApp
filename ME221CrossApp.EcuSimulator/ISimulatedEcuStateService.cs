using ME221CrossApp.Models;

namespace ME221CrossApp.EcuSimulator;

public interface ISimulatedEcuStateService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    EcuInfo GetEcuInfo();
    IReadOnlyList<EcuObjectDefinition> GetObjectList();
    IReadOnlyDictionary<ushort, (ushort Id, byte Type)> GetReportingMap();
    TableData? GetTable(ushort id);
    DriverData? GetDriver(ushort id);
    void UpdateTable(TableData table);
    void UpdateDriver(DriverData driver);
    IReadOnlyList<RealtimeDataPoint> GetAllRealtimeDataAndUpdate();
    IReadOnlyList<EcuObjectDefinition> GetDataLinkList();
}