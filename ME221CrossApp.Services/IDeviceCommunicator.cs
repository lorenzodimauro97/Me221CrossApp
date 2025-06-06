using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public interface IDeviceCommunicator : IAsyncDisposable
{
    Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default);
    Task<Message> SendMessageAsync(Message request, TimeSpan timeout, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Message> GetIncomingMessages(CancellationToken cancellationToken = default);
    bool IsConnected { get; }
}