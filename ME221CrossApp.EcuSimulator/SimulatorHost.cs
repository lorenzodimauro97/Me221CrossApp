using System.Net;
using System.Net.Sockets;
using ME221CrossApp.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.EcuSimulator;

public class SimulatorHost(ISimulatedEcuStateService stateService, IEcuDefinitionService definitionService, ILoggerFactory loggerFactory) : IHostedService
{
    private TcpListener? _listener;
    private readonly ILogger<SimulatorHost> _logger = loggerFactory.CreateLogger<SimulatorHost>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await stateService.InitializeAsync(cancellationToken);
        
        _listener = new TcpListener(IPAddress.Loopback, 54321);
        _listener.Start();
        _logger.LogInformation("ECU Simulator listening on 127.0.0.1:54321...");

        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    _logger.LogInformation("Accepted new client from {RemoteEndPoint}", client.Client.RemoteEndPoint);
                    var handler = new ClientHandler(client, stateService, definitionService, loggerFactory.CreateLogger<ClientHandler>());
                    _ = handler.HandleClientAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting new client.");
                }
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return Task.CompletedTask;
    }
}