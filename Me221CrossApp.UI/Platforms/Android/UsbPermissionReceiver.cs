using Android.App;
using Android.Content;
using Android.Hardware.Usb;

namespace Me221CrossApp.UI.Services;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([UsbConstants.ActionUsbPermission])]
public class UsbPermissionReceiver : BroadcastReceiver
{
    public static TaskCompletionSource<bool>? PermissionTcs;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == UsbConstants.ActionUsbPermission)
        {
            var granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            PermissionTcs?.TrySetResult(granted);
        }
    }
}