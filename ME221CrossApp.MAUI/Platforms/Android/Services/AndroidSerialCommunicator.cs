using System.Collections.Concurrent;
using System.Threading.Channels;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using ME221CrossApp.Models;
using ME221CrossApp.Services;
using ME221CrossApp.Services.Helpers;
using Microsoft.Maui.ApplicationModel;

namespace ME221CrossApp.MAUI.Services;

public sealed class AndroidSerialCommunicator : ISerialPortCommunicator
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

    public bool IsConnected => _connection is not null;

    public async Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        _usbManager = Platform.AppContext.GetSystemService(Context.UsbService) as UsbManager;
        if (_usbManager == null) throw new NotSupportedException("USB Manager not available.");

        _device = _usbManager.DeviceList.Values.FirstOrDefault(d => d.DeviceName == portName);
        if (_device == null) throw new ArgumentException("Specified USB device not found.", nameof(portName));

        if (!_usbManager.HasPermission(_device))
        {
            var tcs = new TaskCompletionSource<bool>();
            const string actionUsbPermission = "com.companyname.me221crossapp.maui.USB_PERMISSION";
            var intent = new Intent(actionUsbPermission);
            var pendingIntent = PendingIntent.GetBroadcast(Platform.AppContext, 0, intent, PendingIntentFlags.Mutable);

            var receiver = new UsbPermissionReceiver(tcs);
            Platform.AppContext.RegisterReceiver(receiver, new IntentFilter(actionUsbPermission));

            _usbManager.RequestPermission(_device, pendingIntent);

            var permissionGranted = await tcs.Task;
            Platform.AppContext.UnregisterReceiver(receiver);

            if (!permissionGranted) throw new UnauthorizedAccessException("USB permission denied.");
        }

        if (!FindEndpointsAndInterface(_device, out var usbInterface))
        {
            throw new InvalidOperationException("Could not find compatible endpoints on the USB device.");
        }

        _connection = _usbManager.OpenDevice(_device);
        if (_connection == null) throw new IOException("Failed to open USB device.");

        _connection.ClaimInterface(usbInterface, true);

        _connection.ControlTransfer((UsbAddressing)0x21, 0x22, 0x1, 0, null, 0, 100);
        var buffer = new byte[7];
        var bitRateBytes = BitConverter.GetBytes(baudRate);
        bitRateBytes.CopyTo(buffer, 0);
        buffer[4] = 0;
        buffer[5] = 0;
        buffer[6] = 8;
        _connection.ControlTransfer((UsbAddressing)0x21, 0x20, 0, 0, buffer, 7, 100);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task PostMessageAsync(Message request, CancellationToken cancellationToken = default)
    {
        if (_connection is null || _outEndpoint is null || _cts is null) throw new InvalidOperationException("Device is not connected.");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var frame = BuildFrame(request);
        await Task.Run(() => _connection.BulkTransfer(_outEndpoint, frame, 0, frame.Length, 500), linkedCts.Token);
    }

    public async Task<Message> SendMessageAsync(Message request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_connection is null || _outEndpoint is null || _cts is null) throw new InvalidOperationException("Device is not connected.");

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
            await Task.Run(() => _connection.BulkTransfer(_outEndpoint, frame, 0, frame.Length, (int)timeout.TotalMilliseconds), linkedCts.Token);
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, linkedCts.Token));
            if (completedTask != tcs.Task) throw new TimeoutException("The operation has timed out.");
            return await tcs.Task;
        }
        finally
        {
            _pendingCommands.TryRemove(correlationId, out _);
        }
    }

    public IAsyncEnumerable<Message> GetIncomingMessages(CancellationToken cancellationToken = default) => _incomingMessageChannel.Reader.ReadAllAsync(cancellationToken);

    private bool FindEndpointsAndInterface(UsbDevice device, out UsbInterface? foundInterface)
    {
        foundInterface = null;
        for (var i = 0; i < device.InterfaceCount; i++)
        {
            var usbInterface = device.GetInterface(i);
            if (usbInterface.InterfaceClass != UsbClass.CdcData && usbInterface.InterfaceClass != UsbClass.Comm && usbInterface.InterfaceClass != UsbClass.VendorSpec)
            {
                continue;
            }

            UsbEndpoint? epIn = null;
            UsbEndpoint? epOut = null;
            for (var j = 0; j < usbInterface.EndpointCount; j++)
            {
                var ep = usbInterface.GetEndpoint(j);
                if (ep?.Type != UsbAddressing.XferBulk) continue;
                if (ep.Address.HasFlag(UsbAddressing.In)) epIn = ep;
                else epOut = ep;
            }

            if (epIn != null && epOut != null)
            {
                _inEndpoint = epIn;
                _outEndpoint = epOut;
                foundInterface = usbInterface;
                return true;
            }
        }
        return false;
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        if (_connection is null || _inEndpoint is null) return;

        var buffer = new byte[4096];
        var streamBuffer = new MemoryStream();

        while (!token.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await Task.Run(() => _connection.BulkTransfer(_inEndpoint, buffer, 0, buffer.Length, 50), token);
                if (bytesRead > 0)
                {
                    await streamBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    ProcessStreamBuffer(streamBuffer);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { await Task.Delay(50, token); }
            catch (Exception) { await Task.Delay(100, token); }
        }
    }

    private void ProcessStreamBuffer(MemoryStream streamBuffer)
    {
        streamBuffer.Position = 0;
        while (streamBuffer.Length - streamBuffer.Position >= 2)
        {
            if (streamBuffer.ReadByte() != SyncByte1 || streamBuffer.ReadByte() != SyncByte2) continue;

            if (streamBuffer.Length - streamBuffer.Position < 6)
            {
                streamBuffer.Position -= 2;
                break;
            }

            var sizeBytes = new byte[2];
            streamBuffer.Read(sizeBytes, 0, 2);
            var payloadSize = (ushort)(sizeBytes[0] | (sizeBytes[1] << 8));
            var messageContentSize = 3 + payloadSize;
            var frameSize = messageContentSize + 2;

            if (streamBuffer.Length - streamBuffer.Position < frameSize)
            {
                streamBuffer.Position -= 4;
                break;
            }

            var messageContent = new byte[messageContentSize];
            streamBuffer.Read(messageContent, 0, messageContentSize);

            var crcBytes = new byte[2];
            streamBuffer.Read(crcBytes, 0, 2);
            var receivedCrc = (ushort)(crcBytes[0] | (crcBytes[1] << 8));
            var expectedCrc = Fletcher16Checksum.Compute(messageContent);
            if (receivedCrc != expectedCrc) continue;

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
                _incomingMessageChannel.Writer.TryWrite(message);
            }
        }

        var remainingData = streamBuffer.GetBuffer().AsSpan((int)streamBuffer.Position, (int)(streamBuffer.Length - streamBuffer.Position)).ToArray();
        streamBuffer.SetLength(0);
        streamBuffer.Write(remainingData, 0, remainingData.Length);
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

    [BroadcastReceiver(Exported = true)]
    private class UsbPermissionReceiver : BroadcastReceiver
    {
        private readonly TaskCompletionSource<bool>? _tcs;

        public UsbPermissionReceiver()
        {
        }

        public UsbPermissionReceiver(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            var granted = intent?.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false) ?? false;
            _tcs?.TrySetResult(granted);
        }
    }
}