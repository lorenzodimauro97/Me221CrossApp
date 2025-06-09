using System.Collections.Concurrent;
using System.Threading.Channels;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using ME221CrossApp.Models;
using ME221CrossApp.Services;
using ME221CrossApp.Services.Helpers;
using Application = Android.App.Application;

namespace Me221CrossApp.UI.Services;

public sealed class AndroidUsbCommunicator : IDeviceCommunicator
{
    private UsbManager? _usbManager;
    private UsbDevice? _device;
    private UsbDeviceConnection? _connection;
    private UsbEndpoint? _inEndpoint;
    private UsbEndpoint? _outEndpoint;

    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Message>> _pendingCommands = new();
    private readonly Channel<Message> _incomingMessageChannel = Channel.CreateUnbounded<Message>();

    private const byte SyncByte1 = (byte)'M';
    private const byte SyncByte2 = (byte)'E';

    public bool IsConnected => _connection is not null && _inEndpoint is not null && _outEndpoint is not null;

    public async Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        _usbManager = Application.Context.GetSystemService(Context.UsbService) as UsbManager;
        if (_usbManager is null)
        {
            throw new InvalidOperationException("USB Manager not available.");
        }

        _device = _usbManager.DeviceList?.Values.FirstOrDefault(d => d.DeviceName == portName);
        if (_device is null)
        {
            throw new InvalidOperationException($"Device {portName} not found.");
        }

        if (!_usbManager.HasPermission(_device))
        {
            var permissionGranted = await RequestPermissionAsync(_usbManager, _device);
            if (!permissionGranted)
            {
                throw new UnauthorizedAccessException("Permission denied for USB device.");
            }
        }
        
        for (var i = 0; i < _device.InterfaceCount; i++)
        {
            var usbInterface = _device.GetInterface(i);
            UsbEndpoint? epIn = null;
            UsbEndpoint? epOut = null;

            for (var j = 0; j < usbInterface.EndpointCount; j++)
            {
                var endpoint = usbInterface.GetEndpoint(j);
                if (endpoint.Type == UsbAddressing.XferBulk)
                {
                    if (endpoint.Direction == UsbAddressing.In)
                    {
                        epIn = endpoint;
                    }
                    else if (endpoint.Direction == UsbAddressing.Out)
                    {
                        epOut = endpoint;
                    }
                }
            }

            if (epIn is null || epOut is null) continue;

            _inEndpoint = epIn;
            _outEndpoint = epOut;
            _connection = _usbManager.OpenDevice(_device);
            _connection.ClaimInterface(usbInterface, true);
            break;
        }

        if (!IsConnected)
        {
            throw new InvalidOperationException("Could not establish a connection to the compatible USB device interface.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    private static async Task<bool> RequestPermissionAsync(UsbManager manager, UsbDevice device)
    {
        UsbPermissionReceiver.PermissionTcs = new TaskCompletionSource<bool>();
        var intent = new Intent(UsbConstants.ActionUsbPermission);
        var pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        manager.RequestPermission(device, pendingIntent);
        return await UsbPermissionReceiver.PermissionTcs.Task;
    }
    
    public async Task PostMessageAsync(Message request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _cts is null) throw new InvalidOperationException("Device is not connected.");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var frame = BuildFrame(request);
        var bytesSent = await _connection!.BulkTransferAsync(_outEndpoint, frame, frame.Length, 500);

        if (bytesSent < frame.Length)
        {
            throw new IOException("Failed to send full message.");
        }
    }

    public async Task<Message> SendMessageAsync(Message request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _cts is null) throw new InvalidOperationException("Device is not connected.");

        var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        var correlationId = (ushort)(request.Class << 8 | request.Command);

        if (!_pendingCommands.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"A command with Class '{request.Class}' and Command '{request.Command}' is already in flight.");
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            await PostMessageAsync(request, linkedCts.Token);
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

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var readBuffer = new byte[4096];
        using var processingStream = new MemoryStream();

        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                var bytesRead = await _connection!.BulkTransferAsync(_inEndpoint, readBuffer, readBuffer.Length, 50);
                if (bytesRead > 0)
                {
                    var currentPosition = processingStream.Position;
                    processingStream.Seek(0, SeekOrigin.End);
                    await processingStream.WriteAsync(readBuffer.AsMemory(0, bytesRead), token);
                    processingStream.Position = currentPosition;
                }

                while (await TryProcessFrame(processingStream, token)) { }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { await Task.Delay(50, token); }
        }
    }

    private async Task<bool> TryProcessFrame(MemoryStream stream, CancellationToken token)
    {
        var startPosition = stream.Position;
        if (stream.Length - startPosition < 7) return false;

        if (stream.ReadByte() != SyncByte1 || stream.ReadByte() != SyncByte2)
        {
            stream.Position = startPosition + 1;
            return true;
        }

        var sizeBytes = new byte[2];
        stream.Read(sizeBytes, 0, 2);
        var payloadSize = (ushort)(sizeBytes[0] | (sizeBytes[1] << 8));
        var messageContentSize = 3 + payloadSize;
        
        if (messageContentSize >= 4096)
        {
             stream.Position = startPosition + 1;
             return true;
        }

        if (stream.Length - stream.Position < messageContentSize + 2)
        {
            stream.Position = startPosition;
            return false;
        }

        var messageContent = new byte[messageContentSize];
        stream.Read(messageContent, 0, messageContentSize);

        var crcBytes = new byte[2];
        stream.Read(crcBytes, 0, 2);
        var receivedCrc = (ushort)(crcBytes[0] | (crcBytes[1] << 8));
        var expectedCrc = Fletcher16Checksum.Compute(messageContent);

        if (receivedCrc != expectedCrc)
        {
            stream.Position = startPosition + 1;
            return true;
        }

        var payload = new byte[payloadSize];
        if (payloadSize > 0) Array.Copy(messageContent, 3, payload, 0, payloadSize);

        var message = new Message(messageContent[0], messageContent[1], messageContent[2], payload);
        var correlationId = (ushort)(message.Class << 8 | message.Command);
        if (_pendingCommands.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetResult(message);
        }
        else
        {
            await _incomingMessageChannel.Writer.WriteAsync(message, token);
        }

        if (stream.Position == stream.Length)
        {
            stream.SetLength(0);
        }
        
        return true;
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

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        _incomingMessageChannel.Writer.TryComplete();
        foreach (var tcs in _pendingCommands.Values) tcs.TrySetCanceled();
        _pendingCommands.Clear();
        _connection?.Close();
        _connection?.Dispose();
    }
}