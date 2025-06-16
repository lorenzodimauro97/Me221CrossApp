using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.Services;

public sealed class TcpDeviceCommunicator(ILogger<TcpDeviceCommunicator> logger)
    : ISerialPortCommunicator, ITcpPortCommunicator
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Message>> _pendingCommands = new();
    private Channel<Message> _incomingMessageChannel = Channel.CreateUnbounded<Message>();

    private const byte SyncByte1 = (byte)'M';
    private const byte SyncByte2 = (byte)'E';

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public async Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        if (_incomingMessageChannel.Reader.Completion.IsCompleted)
        {
            _incomingMessageChannel = Channel.CreateUnbounded<Message>();
        }
        _pendingCommands.Clear();
        
        var parts = portName.Split(':');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip) || !int.TryParse(parts[1], out var port))
        {
            throw new ArgumentException("Invalid endpoint format. Expected 'IP:Port'.", nameof(portName));
        }

        logger.LogInformation("Connecting to TCP device at {Endpoint}", portName);
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(ip, port, cancellationToken);
        _stream = _tcpClient.GetStream();

        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        logger.LogInformation("Successfully connected to {Endpoint}", portName);
    }

    public async Task PostMessageAsync(Message request, CancellationToken cancellationToken = default)
    {
        if (_stream is null || !IsConnected || _cts is null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var frame = BuildFrame(request);
        await _stream.WriteAsync(frame, linkedCts.Token);
    }

    public async Task<Message> SendMessageAsync(Message request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_stream is null || !IsConnected || _cts is null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }
        
        logger.LogTrace("Sending message with Class={Class}, Command={Command}", request.Class, request.Command);
        var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        var correlationId = (ushort)(request.Class << 8 | request.Command);

        if (!_pendingCommands.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"A command with Class '{request.Class}' and Command '{request.Command}' is already in flight.");
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var frame = BuildFrame(request);
            await _stream.WriteAsync(frame, linkedCts.Token);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, linkedCts.Token));

            if (completedTask != tcs.Task)
            {
                logger.LogWarning("Timeout waiting for response to message with Class={Class}, Command={Command}", request.Class, request.Command);
                throw new TimeoutException("The operation has timed out.");
            }

            return await tcs.Task;
        }
        finally
        {
            _pendingCommands.TryRemove(correlationId, out _);
        }
    }

    public IAsyncEnumerable<Message> GetIncomingMessages(CancellationToken cancellationToken = default)
    {
        return _incomingMessageChannel.Reader.ReadAllAsync(cancellationToken);
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

    private async Task ReadLoopAsync(CancellationToken token)
    {
        if (_stream is null) return;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (await ReadByteAsync(_stream, token) != SyncByte1 || await ReadByteAsync(_stream, token) != SyncByte2) continue;

                var sizeBytes = await ReadBytesAsync(_stream, 2, token);
                var payloadSize = (ushort)(sizeBytes[0] | (sizeBytes[1] << 8));
                var messageContentSize = 3 + payloadSize;

                if (messageContentSize >= 4096) continue;

                var messageContent = await ReadBytesAsync(_stream, messageContentSize, token);
                var crcBytes = await ReadBytesAsync(_stream, 2, token);
                var receivedCrc = (ushort)(crcBytes[0] | (crcBytes[1] << 8));
                var expectedCrc = Fletcher16Checksum.Compute(messageContent);

                if (receivedCrc != expectedCrc)
                {
                    logger.LogWarning("Invalid CRC received. Frame dropped.");
                    continue;
                }

                var payload = new byte[payloadSize];
                if (payloadSize > 0)
                {
                    Array.Copy(messageContent, 3, payload, 0, payloadSize);
                }

                var message = new Message(messageContent[0], messageContent[1], messageContent[2], payload);
                var correlationId = (ushort)(message.Class << 8 | message.Command);
                if (_pendingCommands.TryRemove(correlationId, out var tcs))
                {
                    logger.LogTrace("Received response for Class={Class}, Command={Command}", message.Class, message.Command);
                    tcs.TrySetResult(message);
                }
                else
                {
                    logger.LogTrace("Received unsolicited message with Type={Type}, Class={Class}, Command={Command}", message.Type, message.Class, message.Command);
                    await _incomingMessageChannel.Writer.WriteAsync(message, token);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TCP communicator read loop");
                await Task.Delay(100, token);
            }
        }
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
        var offset = 0;
        while (offset < count)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), token);
            if (bytesRead == 0) throw new EndOfStreamException();
            offset += bytesRead;
        }
        return buffer;
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogInformation("Disposing TCP communicator.");
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        _incomingMessageChannel.Writer.TryComplete();

        foreach (var tcs in _pendingCommands.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingCommands.Clear();

        _stream?.Dispose();
        _tcpClient?.Dispose();
        _stream = null;
        _tcpClient = null;

        await ValueTask.CompletedTask;
    }
}