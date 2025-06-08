using System.Net.Sockets;
using ME221CrossApp.EcuSimulator.Helpers;
using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;

namespace ME221CrossApp.EcuSimulator;

public class ClientHandler(TcpClient client, ISimulatedEcuStateService stateService)
{
    private readonly CancellationTokenSource _cts = new();
    private bool _isReportingEnabled;
    private const byte SyncByte1 = (byte)'M';
    private const byte SyncByte2 = (byte)'E';

    public async Task HandleClientAsync()
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        var stream = client.GetStream();

        var readLoopTask = ReadLoopAsync(stream, _cts.Token);
        var realtimeTask = StreamRealtimeDataAsync(stream, _cts.Token);

        await Task.WhenAny(readLoopTask, realtimeTask);

        await _cts.CancelAsync();
        Console.WriteLine($"Client disconnected: {client.Client.RemoteEndPoint}");
        client.Close();
    }

    private async Task StreamRealtimeDataAsync(NetworkStream stream, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(50, token);
            if (!_isReportingEnabled) continue;

            var dataPoints = stateService.GetAllRealtimeDataAndUpdate();
            var payload = SimulatorEcuPayloadBuilder.BuildRealtimeDataPayload(dataPoints, stateService.GetReportingMap());
            var message = new Message(0x0F, 0x00, 0x00, payload);
            var frame = BuildFrame(message);
            await stream.WriteAsync(frame, token);
        }
    }

    private async Task ReadLoopAsync(NetworkStream stream, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (await ReadByteAsync(stream, token) != SyncByte1 || await ReadByteAsync(stream, token) != SyncByte2) continue;

                var sizeBytes = await ReadBytesAsync(stream, 2, token);
                var payloadSize = (ushort)(sizeBytes[0] | (sizeBytes[1] << 8));
                var messageContentSize = 3 + payloadSize;

                if (messageContentSize >= 4096) continue;

                var messageContent = await ReadBytesAsync(stream, messageContentSize, token);
                var crcBytes = await ReadBytesAsync(stream, 2, token);
                var receivedCrc = (ushort)(crcBytes[0] | (crcBytes[1] << 8));
                var expectedCrc = Fletcher16Checksum.Compute(messageContent);

                if (receivedCrc != expectedCrc) continue;

                var payload = new byte[payloadSize];
                if (payloadSize > 0)
                {
                    Array.Copy(messageContent, 3, payload, 0, payloadSize);
                }

                var request = new Message(messageContent[0], messageContent[1], messageContent[2], payload);
                var response = ProcessRequest(request);
                
                if (response.Payload.Length > 0)
                {
                    var responseFrame = BuildFrame(response);
                    await stream.WriteAsync(responseFrame, token);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during client handling: {ex.Message}");
                break;
            }
        }
    }

    private Message ProcessRequest(Message request)
    {
        byte[] responsePayload;

        switch (request)
        {
            case { Class: 0x04, Command: 0x00 }:
                responsePayload = SimulatorEcuPayloadBuilder.BuildGetEcuInfoResponsePayload(stateService.GetEcuInfo());
                break;
            case { Class: 0x04, Command: 0x01 }:
                responsePayload = SimulatorEcuPayloadBuilder.BuildGetObjectListResponsePayload(stateService.GetObjectList());
                break;
            case { Class: 0x01, Command: 0x01 }:
                var tableId = BitConverter.ToUInt16(request.Payload, 0);
                var table = stateService.GetTable(tableId);
                responsePayload = table != null ? SimulatorEcuPayloadBuilder.BuildGetTableResponsePayload(table) : [0x01];
                break;
            case { Class: 0x01, Command: 0x00 }:
                var updatedTable = SimulatorEcuDataParser.ParseSetTablePayload(request.Payload, null!);
                if(updatedTable != null) stateService.UpdateTable(updatedTable);
                responsePayload = [0x00];
                break;
            case { Class: 0x01, Command: 0x06 }:
                responsePayload = [0x00];
                break;
            case { Class: 0x02, Command: 0x01 }:
                var driverId = BitConverter.ToUInt16(request.Payload, 0);
                var driver = stateService.GetDriver(driverId);
                responsePayload = driver != null ? SimulatorEcuPayloadBuilder.BuildGetDriverResponsePayload(driver) : [0x01];
                break;
            case { Class: 0x02, Command: 0x00 }:
                var updatedDriver = SimulatorEcuDataParser.ParseSetDriverPayload(request.Payload, null!);
                if (updatedDriver != null) stateService.UpdateDriver(updatedDriver);
                responsePayload = [0x00];
                break;
            case { Class: 0x02, Command: 0x02 }:
                responsePayload = [0x00];
                break;
            case { Class: 0x00, Command: 0x02 }:
                _isReportingEnabled = request.Payload[0] == 1;
                responsePayload = SimulatorEcuPayloadBuilder.BuildSetStateResponsePayload(stateService.GetReportingMap());
                break;
            case { Type: 0x0F, Class: 0x00, Command: 0x01 }:
                responsePayload = [];
                break;
            default:
                responsePayload = [0x01];
                break;
        }
        return new Message(0x0F, request.Class, request.Command, responsePayload);
    }
    
    private static byte[] BuildFrame(Message message)
    {
        var payloadLength = (ushort)message.Payload.Length;
        var messageContent = new byte[3 + payloadLength];
        messageContent[0] = message.Type;
        messageContent[1] = message.Class;
        messageContent[2] = message.Command;
        message.Payload.CopyTo(messageContent, 3);
        var crc = Fletcher16Checksum.Compute(messageContent);

        var finalFrame = new byte[2 + 2 + messageContent.Length + 2];
        finalFrame[0] = SyncByte1;
        finalFrame[1] = SyncByte2;
        finalFrame[2] = (byte)payloadLength;
        finalFrame[3] = (byte)(payloadLength >> 8);
        messageContent.CopyTo(finalFrame, 4);
        finalFrame[^2] = (byte)crc;
        finalFrame[^1] = (byte)(crc >> 8);
        
        return finalFrame;
    }

    private static async Task<byte> ReadByteAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), token);
        if (bytesRead == 0) throw new EndOfStreamException();
        return buffer[0];
    }
    
    private static async Task<byte[]> ReadBytesAsync(Stream stream, int count, CancellationToken token)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), token);
            if (bytesRead == 0) throw new EndOfStreamException();
            offset += bytesRead;
        }
        return buffer;
    }
}