using System.IO.Ports;
using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public class DesktopDeviceDiscoveryService : IDeviceDiscoveryService
{
    public Task<IReadOnlyList<DiscoveredDevice>> GetAvailableDevicesAsync()
    {
        var devices = SerialPort.GetPortNames()
            .Select(p => new DiscoveredDevice(p, p))
            .ToList();
        return Task.FromResult<IReadOnlyList<DiscoveredDevice>>(devices);
    }
}