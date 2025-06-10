using System.IO.Ports;
using ME221CrossApp.Models;
using ME221CrossApp.Services;

namespace Me221CrossApp.UI.Services.Windows;

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