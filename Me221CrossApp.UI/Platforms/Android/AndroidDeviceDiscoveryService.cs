using Android.Content;
using Android.Hardware.Usb;
using ME221CrossApp.Models;
using ME221CrossApp.Services;
using Application = Android.App.Application;

namespace Me221CrossApp.UI.Services;

public class AndroidDeviceDiscoveryService : IDeviceDiscoveryService
{
    public Task<IReadOnlyList<DiscoveredDevice>> GetAvailableDevicesAsync()
    {
        if (Application.Context.GetSystemService(Context.UsbService) is not UsbManager usbManager)
        {
            return Task.FromResult<IReadOnlyList<DiscoveredDevice>>([]);
        }

        var deviceList = usbManager.DeviceList;
        if (deviceList is null || deviceList.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<DiscoveredDevice>>([]);
        }

        var discoveredDevices = deviceList.Values
            .Select(d => new DiscoveredDevice(
                d.DeviceName,
                $"{d.ProductName ?? "Unknown Device"} (VID:{d.VendorId} PID:{d.ProductId})"))
            .ToList();

        return Task.FromResult<IReadOnlyList<DiscoveredDevice>>(discoveredDevices);
    }
}