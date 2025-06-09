using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public class TcpDeviceDiscoveryService : IDeviceDiscoveryService
{
    public Task<IReadOnlyList<DiscoveredDevice>> GetAvailableDevicesAsync()
    {
        IReadOnlyList<DiscoveredDevice> devices =
        [
            new("127.0.0.1:54321", "ECU Simulator (Local)"),
            new("192.168.1.47:54321", "ECU Simulator (Network)")
        ];
        return Task.FromResult(devices);
    }
}