using Android.Content;
using Android.Hardware.Usb;

namespace Me221CrossApp.UI.Services;

public class UsbPermissionReceiver : BroadcastReceiver
{
    public TaskCompletionSource<bool> PermissionTcs { get; } = new();

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == UsbConstants.ActionUsbPermission)
        {
            var granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            PermissionTcs.TrySetResult(granted);
        }
        
        try
        {
            context?.UnregisterReceiver(this);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            // Receiver might have already been unregistered. Ignore.
        }
    }
}