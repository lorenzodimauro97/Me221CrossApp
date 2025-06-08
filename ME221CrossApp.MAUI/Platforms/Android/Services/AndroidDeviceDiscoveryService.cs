using Android.Content;
using Android.Hardware.Usb;
using ME221CrossApp.Services;
using Microsoft.Maui.ApplicationModel;

namespace ME221CrossApp.MAUI.Platforms.Android.Services;

public class AndroidDeviceDiscoveryService : IDeviceDiscoveryService
{
    public Task<IReadOnlyList<string>> GetAvailablePortsAsync()
    {
        var usbManager = Platform.AppContext.GetSystemService(Context.UsbService) as UsbManager;
        if (usbManager == null)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var deviceList = usbManager.DeviceList.Values
            .Select(d => d.DeviceName)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(deviceList);
    }
}