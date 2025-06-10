using ME221CrossApp.Models;
using ME221CrossApp.Services;

namespace Me221CrossApp.UI.Services;

public enum CommunicatorType
{
    Serial,
    Tcp
}

public class CommunicationMux : IDeviceCommunicator
{
    private readonly ISerialPortCommunicator _serialPortCommunicator;
    private readonly ITcpPortCommunicator _tcpPortCommunicator;
    private IDeviceCommunicator _activeCommunicator;

    public CommunicationMux(ISerialPortCommunicator serialPortCommunicator, ITcpPortCommunicator tcpPortCommunicator)
    {
        _serialPortCommunicator = serialPortCommunicator;
        _tcpPortCommunicator = tcpPortCommunicator;
        _activeCommunicator = _serialPortCommunicator;
    }

    public void SetActiveCommunicator(CommunicatorType type)
    {
        _activeCommunicator = type switch
        {
            CommunicatorType.Tcp => _tcpPortCommunicator,
            _ => _serialPortCommunicator
        };
    }
    
    public bool IsConnected => _activeCommunicator.IsConnected;

    public Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
        => _activeCommunicator.ConnectAsync(portName, baudRate, cancellationToken);

    public Task<Message> SendMessageAsync(Message request, TimeSpan timeout, CancellationToken cancellationToken = default)
        => _activeCommunicator.SendMessageAsync(request, timeout, cancellationToken);

    public Task PostMessageAsync(Message request, CancellationToken cancellationToken = default)
        => _activeCommunicator.PostMessageAsync(request, cancellationToken);

    public IAsyncEnumerable<Message> GetIncomingMessages(CancellationToken cancellationToken = default)
        => _activeCommunicator.GetIncomingMessages(cancellationToken);
    
    public async ValueTask DisposeAsync()
    {
        await _serialPortCommunicator.DisposeAsync();
        await _tcpPortCommunicator.DisposeAsync();
    }
}