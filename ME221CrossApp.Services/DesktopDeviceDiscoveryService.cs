using System.IO.Ports;

namespace ME221CrossApp.Services;

public class DesktopDeviceDiscoveryService : IDeviceDiscoveryService
{
    public Task<IReadOnlyList<string>> GetAvailablePortsAsync()
    {
        return Task.FromResult<IReadOnlyList<string>>(SerialPort.GetPortNames());
    }
}