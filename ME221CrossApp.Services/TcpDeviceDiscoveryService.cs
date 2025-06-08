namespace ME221CrossApp.Services;

public class TcpDeviceDiscoveryService : IDeviceDiscoveryService
{
    public Task<IReadOnlyList<string>> GetAvailablePortsAsync()
    {
        IReadOnlyList<string> ports = ["127.0.0.1:54321", "192.168.1.47:54321"];
        return Task.FromResult(ports);
    }
}