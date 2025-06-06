using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading.Channels;
using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;

namespace ME221CrossApp.Services;

public sealed class DeviceCommunicator : IDeviceCommunicator
{
    private SerialPort? _serialPort;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Message>> _pendingCommands = new();
    private readonly Channel<Message> _incomingMessageChannel = Channel.CreateUnbounded<Message>();

    private const byte SyncByte1 = (byte)'M';
    private const byte SyncByte2 = (byte)'E';

    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        _serialPort = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        _serialPort.Open();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async Task<Message> SendMessageAsync(Message request, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (_serialPort is null || !_serialPort.IsOpen || _cts is null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Command ID is used to correlate response to request
        var correlationId = (ushort)(request.Class << 8 | request.Command);

        if (!_pendingCommands.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException(
                $"A command with Class '{request.Class}' and Command '{request.Command}' is already in flight.");
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var frame = BuildFrame(request);
            await _serialPort.BaseStream.WriteAsync(frame, linkedCts.Token);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, linkedCts.Token));

            if (completedTask != tcs.Task)
            {
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
        var frameSize = 5 + payloadLength;
        var buffer = new byte[frameSize];

        buffer[0] = message.Type;
        buffer[1] = message.Class;
        buffer[2] = message.Command;

        message.Payload.CopyTo(buffer, 3);

        var checksumData = new byte[3 + payloadLength];
        Array.Copy(buffer, 0, checksumData, 0, 3 + payloadLength);
        var crc = Fletcher16Checksum.Compute(checksumData);

        var finalFrame = new byte[2 + 2 + frameSize + 2];
        finalFrame[0] = SyncByte1;
        finalFrame[1] = SyncByte2;
        finalFrame[2] = (byte)frameSize;
        finalFrame[3] = (byte)(frameSize >> 8);

        Array.Copy(buffer, 0, finalFrame, 4, frameSize);

        finalFrame[^2] = (byte)crc;
        finalFrame[^1] = (byte)(crc >> 8);

        return finalFrame;
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        if (_serialPort is null) return;
        var stream = _serialPort.BaseStream;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (await ReadByteAsync(stream, token) != SyncByte1 ||
                    await ReadByteAsync(stream, token) != SyncByte2) continue;
                var sizeBytes = await ReadBytesAsync(stream, 2, token);
                var size = (ushort)(sizeBytes[0] | (sizeBytes[1] << 8));

                if (size is <= 0 or >= 4096) continue;

                var frameData = await ReadBytesAsync(stream, size, token);
                var crcBytes = await ReadBytesAsync(stream, 2, token);
                var receivedCrc = (ushort)(crcBytes[0] | (crcBytes[1] << 8));

                var expectedCrc = Fletcher16Checksum.Compute(frameData);

                if (receivedCrc != expectedCrc) continue;

                var payload = new byte[size - 3];
                Array.Copy(frameData, 3, payload, 0, payload.Length);
                var message = new Message(frameData[0], frameData[1], frameData[2], payload);

                var correlationId = (ushort)(message.Class << 8 | message.Command);
                if (_pendingCommands.TryRemove(correlationId, out var tcs))
                {
                    tcs.TrySetResult(message);
                }
                else
                {
                    await _incomingMessageChannel.Writer.WriteAsync(message, token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // To-do: log errors
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
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        _incomingMessageChannel.Writer.TryComplete();

        foreach (var tcs in _pendingCommands.Values)
        {
            tcs.TrySetCanceled();
        }

        _pendingCommands.Clear();

        if (_serialPort != null)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.Dispose();
        }

        await ValueTask.CompletedTask;
    }
}