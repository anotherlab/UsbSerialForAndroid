using Android.Content;
using Android.Hardware.Usb;
using Android.OS;

namespace UsbSerialForAndroidDemo
{
    public static class UsbManagerExtensions
    {
        const string ACTION_USB_PERMISSION = "com.UsbSerialForAndroidDemo.Util.USB_PERMISSION";

        public static Task<bool> RequestPermissionAsync(this UsbManager manager, UsbDevice device, Context context)
        {
            var completionSource = new TaskCompletionSource<bool>();

            var usbPermissionReceiver = new UsbPermissionReceiver(completionSource);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
#pragma warning disable CA1416
                context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ACTION_USB_PERMISSION), ReceiverFlags.NotExported);
#pragma warning restore CA1416
            }
            else
            {
                context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ACTION_USB_PERMISSION));
            }

            // Targeting S+ (version 31 and above) requires that one of FLAG_IMMUTABLE or FLAG_MUTABLE be specified when creating a PendingIntent.
#pragma warning disable CA1416
            PendingIntentFlags pendingIntentFlags = Build.VERSION.SdkInt >= BuildVersionCodes.S ? PendingIntentFlags.Mutable : 0;
#pragma warning restore CA1416

            var intent = new Intent(ACTION_USB_PERMISSION);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
            {
                intent.SetPackage(context.PackageName);
            }
            var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, pendingIntentFlags);

            manager.RequestPermission(device, pendingIntent);

            return completionSource.Task;
        }
    }
}
