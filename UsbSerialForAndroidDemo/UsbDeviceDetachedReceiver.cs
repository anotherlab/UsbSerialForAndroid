using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;

namespace UsbSerialForAndroidDemo
{
    public class UsbDeviceDetachedReceiver(MainActivity activity) : BroadcastReceiver
    {
        readonly string TAG = typeof(UsbDeviceDetachedReceiver).Name;
        readonly MainActivity activity = activity;

        public override async void OnReceive(Context? context, Intent? intent)
        {
            UsbDevice? device;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // Android 33
            {
#pragma warning disable CA1416 // Suppress warning to validate platform compatibility
                device = intent!.GetParcelableExtra(UsbManager.ExtraDevice, Java.Lang.Class.FromType(typeof(UsbDevice))) as UsbDevice;
#pragma warning restore CA1416
            }
            else
            {
#pragma warning disable CA1422 // Suppress warning for obsolete API usage
                device = intent!.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
#pragma warning restore CA1422
            }

            if (device != null)
            {
                Log.Info(TAG, $"{context!.GetString(Resource.String.usb_device_detached)} : {device.DeviceName}");
                await activity.PopulateDeviceListAsync();
            }
        }
    }
}