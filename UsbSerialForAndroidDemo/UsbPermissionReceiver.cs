using Android.Content;
using Android.Hardware.Usb;
using Android.OS;

namespace UsbSerialForAndroidDemo
{
    public class UsbPermissionReceiver : BroadcastReceiver
    {
        readonly TaskCompletionSource<bool> completionSource;

        public UsbPermissionReceiver(TaskCompletionSource<bool> completionSource)
        {
            this.completionSource = completionSource;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
#pragma warning disable CA1416
                var device = intent!.GetParcelableExtra(UsbManager.ExtraDevice, Java.Lang.Class.FromType(typeof(UsbDevice)));
#pragma warning restore CA1416
            }
            else
            {
#pragma warning disable CA1422
                var device = intent!.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
#pragma warning restore CA1422
            }

            var permissionGranted = intent!.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            context!.UnregisterReceiver(this);
            completionSource.TrySetResult(permissionGranted);
        }
    }
}
