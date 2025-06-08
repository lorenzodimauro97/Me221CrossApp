using ME221CrossApp.Models;
using ME221CrossApp.Services;

namespace ME221CrossApp.MAUI.Services;

public sealed class HybridDeviceCommunicator(
    ConnectionState connectionState,
    ISerialPortCommunicator serialCommunicator,
    ITcpPortCommunicator tcpCommunicator) : IDeviceCommunicator
{
    private IDeviceCommunicator? _activeCommunicator;

    public bool IsConnected => _activeCommunicator?.IsConnected ?? false;

    private IDeviceCommunicator GetActiveCommunicator()
    {
        if (!connectionState.IsModeSelected)
        {
            throw new InvalidOperationException("Connection mode has not been selected.");
        }

        _activeCommunicator = connectionState.Mode switch
        {
            ConnectionMode.Serial => serialCommunicator,
            ConnectionMode.Tcp => tcpCommunicator,
            _ => throw new InvalidOperationException("Unsupported connection mode.")
        };

        return _activeCommunicator;
    }

    public Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        var communicator = GetActiveCommunicator();
        return communicator.ConnectAsync(portName, baudRate, cancellationToken);
    }

    public Task<Message> SendMessageAsync(Message request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var communicator = GetActiveCommunicator();
        return communicator.SendMessageAsync(request, timeout, cancellationToken);
    }

    public Task PostMessageAsync(Message request, CancellationToken cancellationToken = default)
    {
        var communicator = GetActiveCommunicator();
        return communicator.PostMessageAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<Message> GetIncomingMessages(CancellationToken cancellationToken = default)
    {
        var communicator = GetActiveCommunicator();
        return communicator.GetIncomingMessages(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await serialCommunicator.DisposeAsync();
        await tcpCommunicator.DisposeAsync();
    }
}