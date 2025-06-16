using System.Collections.Concurrent;
using ME221CrossApp.Models;
using ME221CrossApp.Services;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.EcuSimulator;

public class SimulatedEcuStateService(IEcuDefinitionService definitionService, ILogger<SimulatedEcuStateService> logger)
    : ISimulatedEcuStateService
{
    private readonly ConcurrentDictionary<ushort, TableData> _tables = new();
    private readonly ConcurrentDictionary<ushort, DriverData> _drivers = new();
    private readonly ConcurrentDictionary<ushort, RealtimeDataPoint> _realtimeData = new();
    private readonly EcuInfo _ecuInfo = new("ME221-SIM", "PnP", "SIM-1.0", "SIM-FW-1.0", Guid.NewGuid().ToString(), "0000");
    private double _angle;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing simulated ECU state...");
        await definitionService.LoadFromStoreAsync(cancellationToken);
        var definition = definitionService.GetDefinition();
        if (definition is null)
        {
            logger.LogWarning("ECU definition is null during initialization.");
            return;
        }

        foreach (var ecuObject in definition.EcuObjects.Values)
        {
            switch (ecuObject.ObjectType)
            {
                case "Table":
                    var rows = ecuObject.Rows ?? 1;
                    var cols = ecuObject.Cols ?? 16;
                    var is3D = rows > 1;
                    
                    var xAxis = Enumerable.Range(0, cols).Select(i => i * 500f).ToArray();
                    var yAxis = is3D ? Enumerable.Range(0, rows).Select(i => i * 10f).ToArray() : [];
                    var output = new float[rows * cols];

                    for (var r = 0; r < rows; r++)
                    {
                        for (var c = 0; c < cols; c++)
                        {
                            var outputIndex = r * cols + c;
                            if (is3D)
                            {
                                output[outputIndex] = (float)(Math.Sin(c / (cols - 1.0) * Math.PI) * Math.Cos(r / (rows - 1.0) * Math.PI) * 50 + 50);
                            }
                            else
                            {
                                output[outputIndex] = (float)(Math.Sin(c / (cols - 1.0) * Math.PI) * 50 + 50);
                            }
                        }
                    }
                    
                    _tables[ecuObject.Id] = new TableData(ecuObject.Id, ecuObject.Name, 0, true, rows, cols, xAxis, yAxis, output);
                    break;
                case "Driver":
                    var numConfigs = ecuObject.Parameters?.Count ?? 0;
                    var configParams = Enumerable.Range(0, numConfigs).Select(i => (float)(i * 1.5)).ToList();
                    var numInputs = ecuObject.InputLinks?.Names.Count ?? 0;
                    var inputIds = Enumerable.Range(1, numInputs).Select(i => (ushort)i).ToList();
                    var numOutputs = ecuObject.OutputLinks?.Names.Count ?? 0;
                    var outputIds = Enumerable.Range(10, numOutputs).Select(i => (ushort)i).ToList();
                    _drivers[ecuObject.Id] = new DriverData(ecuObject.Id, ecuObject.Name, 
                        configParams, inputIds, outputIds);
                    break;
                case "DataLink":
                    _realtimeData[ecuObject.Id] = new RealtimeDataPoint(ecuObject.Id, ecuObject.Name, 0);
                    break;
            }
        }
        logger.LogInformation("Simulated ECU state initialized with {TableCount} tables, {DriverCount} drivers, and {DataLinkCount} data links.", _tables.Count, _drivers.Count, _realtimeData.Count);
    }

    public EcuInfo GetEcuInfo() => _ecuInfo;

    public IReadOnlyList<EcuObjectDefinition> GetObjectList()
    {
        return definitionService.GetDefinition()?.EcuObjects.Values
            .Where(o => o.ObjectType is "Table" or "Driver")
            .ToList() ?? [];
    }
    
    public IReadOnlyList<EcuObjectDefinition> GetDataLinkList()
    {
        return definitionService.GetDefinition()?.EcuObjects.Values
            .Where(o => o.ObjectType is "DataLink")
            .ToList() ?? [];
    }

    public IReadOnlyDictionary<ushort, (ushort Id, byte Type)> GetReportingMap()
    {
        return _realtimeData.Keys.ToDictionary(id => id, id => (id, (byte)(id % 6)));
    }

    public TableData? GetTable(ushort id) => _tables.GetValueOrDefault(id);

    public DriverData? GetDriver(ushort id) => _drivers.GetValueOrDefault(id);

    public void UpdateTable(TableData table)
    {
        logger.LogDebug("Updating table {TableId}", table.Id);
        _tables[table.Id] = table;
    }

    public void UpdateDriver(DriverData driver)
    {
        logger.LogDebug("Updating driver {DriverId}", driver.Id);
        _drivers[driver.Id] = driver;
    }

    public IReadOnlyList<RealtimeDataPoint> GetAllRealtimeDataAndUpdate()
    {
        _angle += 0.05;
        if (_angle > 2 * Math.PI) _angle = 0;

        var dataPoints = _realtimeData.Values.ToList();
        for (var i = 0; i < dataPoints.Count; i++)
        {
            var dp = dataPoints[i];
            float value;
            if (dp.Name.Equals("RPM", StringComparison.OrdinalIgnoreCase))
            {
                value = (float)(Math.Sin(_angle) * 3100 + 3900);
            }
            else
            {
                var phase = i * 0.2f;
                float amplitude = 50 + (i % 5 * 20);
                value = (float)(Math.Sin(_angle + phase) * amplitude + amplitude);
            }
            _realtimeData[dp.Id] = dp with { Value = value };
        }
        
        return _realtimeData.Values.ToList();
    }
}