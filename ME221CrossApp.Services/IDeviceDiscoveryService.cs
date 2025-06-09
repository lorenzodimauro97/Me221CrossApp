using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public interface IDeviceDiscoveryService
{
    Task<IReadOnlyList<DiscoveredDevice>> GetAvailableDevicesAsync();
}