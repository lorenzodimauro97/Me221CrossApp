using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;
using System.Runtime.CompilerServices;

namespace ME221CrossApp.Services;

public class EcuInteractionService : IEcuInteractionService
{
    private readonly IDeviceCommunicator _communicator;
    private readonly IEcuDefinitionService _definitionService;

    public EcuInteractionService(IDeviceCommunicator communicator, IEcuDefinitionService definitionService)
    {
        _communicator = communicator;
        _definitionService = definitionService;
    }

    public async Task<EcuInfo?> GetEcuInfoAsync(CancellationToken cancellationToken = default)
    {
        var request = new Message(0x00, 0x04, 0x00, []);
        var response = await _communicator.SendMessageAsync(request, TimeSpan.FromSeconds(2), cancellationToken);
        return response.Payload.Length > 1 ? EcuDataParser.ParseEcuInfo(response.Payload) : null;
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetObjectListAsync(CancellationToken cancellationToken = default)
    {
        var request = new Message(0x00, 0x04, 0x01, [1]);
        var response = await _communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseObjectList(response.Payload, _definitionService);
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetDataLinkListAsync(CancellationToken cancellationToken = default)
    {
        var enableRequest = new Message(0x00, 0x00, 0x02, [1]);
        var response = await _communicator.SendMessageAsync(enableRequest, TimeSpan.FromSeconds(2), cancellationToken);
        var reportingMap = EcuDataParser.ParseSetStateResponse(response.Payload);

        var disableRequest = new Message(0x00, 0x00, 0x02, [0]);
        await _communicator.PostMessageAsync(disableRequest, cancellationToken);
        await Task.Delay(100, cancellationToken); // Give ECU time to process disable command

        var dataLinks = new List<EcuObjectDefinition>();
        foreach (var (id, _) in reportingMap)
        {
            if (_definitionService.TryGetObject(id, out var def) && def is not null)
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
        var response = await _communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseTableData(response.Payload, _definitionService);
    }
    
    public async Task<DriverData?> GetDriverAsync(ushort driverId, CancellationToken cancellationToken = default)
    {
        var payload = BitConverter.GetBytes(driverId);
        var request = new Message(0x00, 0x02, 0x01, payload);
        var response = await _communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), cancellationToken);
        return EcuDataParser.ParseDriverData(response.Payload, _definitionService);
    }
    
    public async IAsyncEnumerable<IReadOnlyList<RealtimeDataPoint>> StreamRealtimeDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enableRequest = new Message(0x00, 0x00, 0x02, [1]);
        var enableResponse = await _communicator.SendMessageAsync(enableRequest, TimeSpan.FromSeconds(2), cancellationToken);
        var reportingMap = EcuDataParser.ParseSetStateResponse(enableResponse.Payload);

        if (reportingMap.Count == 0)
        {
            yield break;
        }

        try
        {
            await foreach (var message in _communicator.GetIncomingMessages(cancellationToken))
            {
                if (message.Type == 0x0F && message.Class == 0x00 && message.Command == 0x00)
                {
                    yield return EcuDataParser.ParseRealtimeData(message.Payload, reportingMap, _definitionService);
                }
            }
        }
        finally
        {
            var disableRequest = new Message(0x00, 0x00, 0x02, [0]);
            await _communicator.PostMessageAsync(disableRequest, CancellationToken.None);
            await Task.Delay(100, CancellationToken.None);
        }
    }
}