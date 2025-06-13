using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.Services;

public class EcuInteractionService(IDeviceCommunicator communicator, IEcuDefinitionService definitionService, ILogger<EcuInteractionService> logger)
    : IEcuInteractionService
{
    public async Task<EcuInfo?> GetEcuInfoAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting ECU info...");
        var request = new Message(0x00, 0x04, 0x00, []);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(2), cancellationToken);
        var ecuInfo = response.Payload.Length > 1 ? EcuDataParser.ParseEcuInfo(response.Payload) : null;
        if (ecuInfo is not null)
        {
            logger.LogInformation("Received ECU info: {ProductName} FW: {FirmwareVersion}", ecuInfo.ProductName, ecuInfo.FirmwareVersion);
        }
        else
        {
            logger.LogWarning("Failed to parse ECU info from response.");
        }
        return ecuInfo;
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetObjectListAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting object list...");
        var request = new Message(0x00, 0x04, 0x01, [1]);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        var list = EcuDataParser.ParseObjectList(response.Payload, definitionService);
        logger.LogInformation("Received {Count} objects in object list.", list.Count);
        return list;
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetDataLinkListAsync(bool keepStreamActive = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting data link list...");
        var enableRequest = new Message(0x00, 0x00, 0x02, [1]);
        var response = await communicator.SendMessageAsync(enableRequest, TimeSpan.FromSeconds(2), cancellationToken);
        var reportingMap = EcuDataParser.ParseSetStateResponse(response.Payload);

        if (!keepStreamActive)
        {
            var disableRequest = new Message(0x00, 0x00, 0x02, [0]);
            await communicator.PostMessageAsync(disableRequest, cancellationToken);
            await Task.Delay(100, cancellationToken);
        }

        var dataLinks = new List<EcuObjectDefinition>();
        foreach (var (id, _) in reportingMap)
        {
            if (definitionService.TryGetObject(id, out var def) && def is not null)
            {
                dataLinks.Add(def);
            }
        }
        logger.LogInformation("Received {Count} data links.", dataLinks.Count);
        return dataLinks;
    }
    
    public async Task<RealtimeDataPoint?> GetRealtimeDataValueAsync(ushort dataLinkId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting single realtime data value for ID {DataLinkId}", dataLinkId);
        await using var stream = StreamRealtimeDataAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        
        if (await stream.MoveNextAsync())
        {
            return stream.Current.FirstOrDefault(dp => dp.Id == dataLinkId);
        }
        
        return null;
    }
    
    public async Task<TableData?> GetTableAsync(ushort tableId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting table data for ID {TableId}", tableId);
        var payload = BitConverter.GetBytes(tableId);
        var request = new Message(0x00, 0x01, 0x01, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseTableData(response.Payload, definitionService);
    }
    
    public async Task<DriverData?> GetDriverAsync(ushort driverId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting driver data for ID {DriverId}", driverId);
        var payload = BitConverter.GetBytes(driverId);
        var request = new Message(0x00, 0x02, 0x01, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseDriverData(response.Payload, definitionService);
    }
    
    public async IAsyncEnumerable<IReadOnlyList<RealtimeDataPoint>> StreamRealtimeDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting real-time data stream.");
        var enableRequest = new Message(0x00, 0x00, 0x02, [1]);
        var enableResponse = await communicator.SendMessageAsync(enableRequest, TimeSpan.FromSeconds(2), cancellationToken);
        var reportingMap = EcuDataParser.ParseSetStateResponse(enableResponse.Payload);

        if (reportingMap.Count == 0)
        {
            logger.LogWarning("Real-time data stream reporting map is empty. Stopping stream.");
            yield break;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var keepAliveTask = Task.Run(async () =>
        {
            var ackMessage = new Message(0x0F, 0x00, 0x01, [0x00]);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(linkedCts.Token))
                {
                    await communicator.PostMessageAsync(ackMessage, linkedCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, linkedCts.Token);

        try
        {
            await foreach (var message in communicator.GetIncomingMessages(linkedCts.Token))
            {
                if (message.Type == 0x0F && message.Class == 0x00 && message.Command == 0x00)
                {
                    yield return EcuDataParser.ParseRealtimeData(message.Payload, reportingMap, definitionService);
                }
            }
        }
        finally
        {
            await linkedCts.CancelAsync();
            try
            {
                await keepAliveTask;
            }
            catch (OperationCanceledException) { }

            logger.LogInformation("Stopping real-time data stream.");
            var disableRequest = new Message(0x00, 0x00, 0x02, [0]);
            await communicator.PostMessageAsync(disableRequest, CancellationToken.None);
            await Task.Delay(100, CancellationToken.None);
        }
    }

    public async Task UpdateTableAsync(TableData table, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating table {TableId} ({TableName})", table.Id, table.Name);
        var payload = EcuDataBuilder.BuildSetTablePayload(table);
        var request = new Message(0x00, 0x01, 0x00, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        
        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            logger.LogError("ECU rejected table update for table {TableId}", table.Id);
            throw new InvalidOperationException("ECU rejected table update.");
        }
    }

    public async Task StoreTableAsync(ushort tableId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Storing table {TableId} to ECU flash.", tableId);
        var payload = BitConverter.GetBytes(tableId);
        var request = new Message(0x00, 0x01, 0x06, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);

        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            logger.LogError("ECU rejected store table command for table {TableId}", tableId);
            throw new InvalidOperationException("ECU rejected store table command.");
        }
    }

    public async Task UpdateDriverAsync(DriverData driver, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating driver {DriverId} ({DriverName})", driver.Id, driver.Name);
        var payload = EcuDataBuilder.BuildSetDriverPayload(driver);
        var request = new Message(0x00, 0x02, 0x00, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        
        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            logger.LogError("ECU rejected driver update for driver {DriverId}", driver.Id);
            throw new InvalidOperationException("ECU rejected driver update.");
        }
    }

    public async Task StoreDriverAsync(ushort driverId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Storing driver {DriverId} to ECU flash.", driverId);
        var payload = BitConverter.GetBytes(driverId);
        var request = new Message(0x00, 0x02, 0x02, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);

        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            logger.LogError("ECU rejected store driver command for driver {DriverId}", driverId);
            throw new InvalidOperationException("ECU rejected store driver command.");
        }
    }
}