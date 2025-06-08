using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

namespace ME221CrossApp.EcuSimulator;

public class SimulatorHost(ISimulatedEcuStateService stateService) : IHostedService
{
    private TcpListener? _listener;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await stateService.InitializeAsync(cancellationToken);
        
        _listener = new TcpListener(IPAddress.Loopback, 54321);
        _listener.Start();
        Console.WriteLine("ECU Simulator listening on 127.0.0.1:54321...");

        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var handler = new ClientHandler(client, stateService);
                _ = handler.HandleClientAsync();
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return Task.CompletedTask;
    }
}