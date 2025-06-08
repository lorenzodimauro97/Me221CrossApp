using System.Collections.Concurrent;
using ME221CrossApp.Models;
using ME221CrossApp.Services;

namespace ME221CrossApp.EcuSimulator;

public class SimulatedEcuStateService : ISimulatedEcuStateService
{
    private readonly IEcuDefinitionService _definitionService;
    private readonly ConcurrentDictionary<ushort, TableData> _tables = new();
    private readonly ConcurrentDictionary<ushort, DriverData> _drivers = new();
    private readonly ConcurrentDictionary<ushort, RealtimeDataPoint> _realtimeData = new();
    private readonly EcuInfo _ecuInfo = new("ME221-SIM", "PnP", "SIM-1.0", "SIM-FW-1.0", Guid.NewGuid().ToString(), "0000");
    private double _angle;

    public SimulatedEcuStateService(IEcuDefinitionService definitionService)
    {
        _definitionService = definitionService;
    }
    
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _definitionService.LoadFromStoreAsync(cancellationToken);
        var definition = _definitionService.GetDefinition();
        if (definition is null) return;

        foreach (var ecuObject in definition.EcuObjects.Values)
        {
            switch (ecuObject.ObjectType)
            {
                case "Table":
                    var xAxis = Enumerable.Range(0, 16).Select(i => i * 500f).ToArray();
                    var yAxis = Enumerable.Range(0, 16).Select(i => i * 10f).ToArray();
                    var output = new float[1 * 16];
                    for (int i = 0; i < 16; i++)
                    {
                        output[i] = (float)(Math.Sin(i / 15.0 * Math.PI) * 50 + 50);
                    }
                    _tables[ecuObject.Id] = new TableData(ecuObject.Id, ecuObject.Name, 0, true, 1, 16, xAxis, [], output);
                    break;
                case "Driver":
                    var configParams = Enumerable.Range(0, 8).Select(i => (float)(i * 1.5)).ToList();
                    var inputIds = Enumerable.Range(1, 4).Select(i => (ushort)i).ToList();
                    var outputIds = Enumerable.Range(10, 4).Select(i => (ushort)i).ToList();
                    _drivers[ecuObject.Id] = new DriverData(ecuObject.Id, ecuObject.Name, 
                        configParams, inputIds, outputIds);
                    break;
                case "DataLink":
                    _realtimeData[ecuObject.Id] = new RealtimeDataPoint(ecuObject.Id, ecuObject.Name, 0);
                    break;
            }
        }
    }

    public EcuInfo GetEcuInfo() => _ecuInfo;

    public IReadOnlyList<EcuObjectDefinition> GetObjectList()
    {
        return _definitionService.GetDefinition()?.EcuObjects.Values
            .Where(o => o.ObjectType is "Table" or "Driver")
            .ToList() ?? [];
    }
    
    public IReadOnlyList<EcuObjectDefinition> GetDataLinkList()
    {
        return _definitionService.GetDefinition()?.EcuObjects.Values
            .Where(o => o.ObjectType is "DataLink")
            .ToList() ?? [];
    }

    public IReadOnlyDictionary<ushort, (ushort Id, byte Type)> GetReportingMap()
    {
        // Simulate a mix of data types for reporting
        return _realtimeData.Keys.ToDictionary(id => id, id => (id, (byte)(id % 6)));
    }

    public TableData? GetTable(ushort id) => _tables.GetValueOrDefault(id);

    public DriverData? GetDriver(ushort id) => _drivers.GetValueOrDefault(id);

    public void UpdateTable(TableData table) => _tables[table.Id] = table;

    public void UpdateDriver(DriverData driver) => _drivers[driver.Id] = driver;

    public IReadOnlyList<RealtimeDataPoint> GetAllRealtimeDataAndUpdate()
    {
        _angle += 0.05;
        if (_angle > 2 * Math.PI) _angle = 0;

        var dataPoints = _realtimeData.Values.ToList();
        for (int i = 0; i < dataPoints.Count; i++)
        {
            var dp = dataPoints[i];
            float phase = i * 0.2f;
            float amplitude = 50 + (i % 5 * 20);
            float value = (float)(Math.Sin(_angle + phase) * amplitude + amplitude);
            _realtimeData[dp.Id] = dp with { Value = value };
        }
        
        return _realtimeData.Values.ToList();
    }
}