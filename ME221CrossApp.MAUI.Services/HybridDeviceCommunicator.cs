using ME221CrossApp.Models;
using ME221CrossApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ME221CrossApp.MAUI.Services;

public sealed class HybridDeviceCommunicator(IServiceProvider serviceProvider, ConnectionState connectionState) : IDeviceCommunicator
{
    private IDeviceCommunicator? _activeCommunicator;
    private ConnectionMode? _activeMode;

    public bool IsConnected => _activeCommunicator?.IsConnected ?? false;

    private IDeviceCommunicator GetActiveCommunicator()
    {
        if (!connectionState.IsModeSelected)
        {
            throw new InvalidOperationException("Connection mode has not been selected.");
        }

        if (_activeCommunicator is null || _activeMode != connectionState.Mode)
        {
            _activeCommunicator?.DisposeAsync().AsTask().GetAwaiter().GetResult();

            _activeCommunicator = connectionState.Mode switch
            {
                ConnectionMode.Serial => serviceProvider.GetRequiredService<DeviceCommunicator>(),
                ConnectionMode.Tcp => serviceProvider.GetRequiredService<TcpDeviceCommunicator>(),
                _ => throw new InvalidOperationException("Unsupported connection mode.")
            };
            _activeMode = connectionState.Mode;
        }
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
        if (_activeCommunicator is not null)
        {
            await _activeCommunicator.DisposeAsync();
        }
    }
}