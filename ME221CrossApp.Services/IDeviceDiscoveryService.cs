namespace ME221CrossApp.Services;

public interface IDeviceDiscoveryService
{
    Task<IReadOnlyList<string>> GetAvailablePortsAsync();
}