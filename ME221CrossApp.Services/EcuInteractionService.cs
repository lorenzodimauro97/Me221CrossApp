using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;
using System.Runtime.CompilerServices;

namespace ME221CrossApp.Services;

public class EcuInteractionService(IDeviceCommunicator communicator, IEcuDefinitionService definitionService)
    : IEcuInteractionService
{
    public async Task<EcuInfo?> GetEcuInfoAsync(CancellationToken cancellationToken = default)
    {
        var request = new Message(0x00, 0x04, 0x00, []);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(2), cancellationToken);
        return response.Payload.Length > 1 ? EcuDataParser.ParseEcuInfo(response.Payload) : null;
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetObjectListAsync(CancellationToken cancellationToken = default)
    {
        var request = new Message(0x00, 0x04, 0x01, [1]);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseObjectList(response.Payload, definitionService);
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetDataLinkListAsync(CancellationToken cancellationToken = default)
    {
        var enableRequest = new Message(0x00, 0x00, 0x02, [1]);
        var response = await communicator.SendMessageAsync(enableRequest, TimeSpan.FromSeconds(2), cancellationToken);
        var reportingMap = EcuDataParser.ParseSetStateResponse(response.Payload);

        var disableRequest = new Message(0x00, 0x00, 0x02, [0]);
        await communicator.PostMessageAsync(disableRequest, cancellationToken);
        await Task.Delay(100, cancellationToken);

        var dataLinks = new List<EcuObjectDefinition>();
        foreach (var (id, _) in reportingMap)
        {
            if (definitionService.TryGetObject(id, out var def) && def is not null)
            {
                dataLinks.Add(def);
            }
        }
        return dataLinks;
    }
    
    public async Task<RealtimeDataPoint?> GetRealtimeDataValueAsync(ushort dataLinkId, CancellationToken cancellationToken = default)
    {
        await using var stream = StreamRealtimeDataAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        
        if (await stream.MoveNextAsync())
        {
            return stream.Current.FirstOrDefault(dp => dp.Id == dataLinkId);
        }
        
        return null;
    }
    
    public async Task<TableData?> GetTableAsync(ushort tableId, CancellationToken cancellationToken = default)
    {
        var payload = BitConverter.GetBytes(tableId);
        var request = new Message(0x00, 0x01, 0x01, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseTableData(response.Payload, definitionService);
    }
    
    public async Task<DriverData?> GetDriverAsync(ushort driverId, CancellationToken cancellationToken = default)
    {
        var payload = BitConverter.GetBytes(driverId);
        var request = new Message(0x00, 0x02, 0x01, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseDriverData(response.Payload, definitionService);
    }
    
    public async IAsyncEnumerable<IReadOnlyList<RealtimeDataPoint>> StreamRealtimeDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enableRequest = new Message(0x00, 0x00, 0x02, [1]);
        var enableResponse = await communicator.SendMessageAsync(enableRequest, TimeSpan.FromSeconds(2), cancellationToken);
        var reportingMap = EcuDataParser.ParseSetStateResponse(enableResponse.Payload);

        if (reportingMap.Count == 0)
        {
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

            var disableRequest = new Message(0x00, 0x00, 0x02, [0]);
            await communicator.PostMessageAsync(disableRequest, CancellationToken.None);
            await Task.Delay(100, CancellationToken.None);
        }
    }

    public async Task UpdateTableAsync(TableData table, CancellationToken cancellationToken = default)
    {
        var payload = EcuDataBuilder.BuildSetTablePayload(table);
        var request = new Message(0x00, 0x01, 0x00, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        
        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            throw new InvalidOperationException("ECU rejected table update.");
        }
    }

    public async Task StoreTableAsync(ushort tableId, CancellationToken cancellationToken = default)
    {
        var payload = BitConverter.GetBytes(tableId);
        var request = new Message(0x00, 0x01, 0x06, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);

        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            throw new InvalidOperationException("ECU rejected store table command.");
        }
    }

    public async Task UpdateDriverAsync(DriverData driver, CancellationToken cancellationToken = default)
    {
        var payload = EcuDataBuilder.BuildSetDriverPayload(driver);
        var request = new Message(0x00, 0x02, 0x00, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        
        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            throw new InvalidOperationException("ECU rejected driver update.");
        }
    }

    public async Task StoreDriverAsync(ushort driverId, CancellationToken cancellationToken = default)
    {
        var payload = BitConverter.GetBytes(driverId);
        var request = new Message(0x00, 0x02, 0x02, payload);
        var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);

        if (response.Payload.Length < 1 || response.Payload[0] != 0)
        {
            throw new InvalidOperationException("ECU rejected store driver command.");
        }
    }
}