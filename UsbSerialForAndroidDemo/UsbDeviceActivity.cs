using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Extensions;
using Hoho.Android.UsbSerial.Util;
using System;
using System.Reflection.Metadata.Ecma335;
using static Android.Icu.Text.IDNA;

namespace UsbSerialForAndroidDemo;

[Activity(Label = "USB Device Details")]
public class UsbDeviceActivity : Activity
{
    static readonly string TAG = typeof(UsbDeviceActivity).Name;
    public const string EXTRA_USB_DEVICE = "UsbDevice";
    const int READ_WAIT_MILLIS = 200;
    const int WRITE_WAIT_MILLIS = 200;

    TextView? DeviceInfoText => FindViewById<TextView>(Resource.Id.deviceInfoText);
    Button? ConnectButton => FindViewById<Button>(Resource.Id.connectButton);
    TextView? StatusText => FindViewById<TextView>(Resource.Id.statusText);
    ScrollView? StatusScrollView => FindViewById<ScrollView>(Resource.Id.statusScrollView)!;

    UsbSerialPortInfo? portInfo;
    UsbManager? usbManager;
    SerialInputOutputManager? serialIoManager;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SetContentView(Resource.Layout.activity_usb_device);

        // Add top padding to account for status bar
        var rootView = FindViewById<Android.Views.ViewGroup>(Android.Resource.Id.Content);
        if (rootView != null)
        {
            var statusBarHeight = GetStatusBarHeight();
            rootView.SetPadding(0, statusBarHeight, 0, 0);
        }

        usbManager = GetSystemService(Context.UsbService) as UsbManager;

        // Get the USB device info from intent
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // Android 33
        {
#pragma warning disable CA1416 // Suppress warning to validate platform compatibility
            portInfo = Intent!.GetParcelableExtra(EXTRA_USB_DEVICE, Java.Lang.Class.FromType(typeof(UsbSerialPortInfo))) as UsbSerialPortInfo;
#pragma warning restore CA1416
        }
        else
        {
#pragma warning disable CA1422 // Suppress warning for obsolete API usage
            portInfo = Intent!.GetParcelableExtra(EXTRA_USB_DEVICE) as UsbSerialPortInfo;
#pragma warning restore CA1422
        }

        if (portInfo != null)
        {
            DisplayDeviceInfo();
        }
        else
        {
            StatusText!.Text = GetString(Resource.String.status_no_device_information);
            ConnectButton!.Enabled = false;
        }

        ConnectButton!.Click += async (sender, e) =>
        {
            await ConnectToDevice();
        };
    }

    int GetStatusBarHeight()
    {
        int result = 0;
        int resourceId = Resources?.GetIdentifier("status_bar_height", "dimen", "android") ?? 0;
        if (resourceId > 0)
        {
            result = Resources?.GetDimensionPixelSize(resourceId) ?? 0;
        }
        return result;
    }

    void DisplayDeviceInfo()
    {
        if (portInfo != null)
        {
            var info = $"{GetString(Resource.String.vendor_id)}: 0x{portInfo.VendorId:X4}\n" +
                      $"{GetString(Resource.String.device_id)}: 0x{portInfo.DeviceId:X4}\n" +
                      $"{GetString(Resource.String.port_number)}: {portInfo.PortNumber}\n" +
                      $"{GetString(Resource.String.device_name)}: {portInfo.DeviceName}";

            DeviceInfoText!.Text = info;
            StatusText!.Text = GetString(Resource.String.status_ready_to_connect);
        }
        else
        {
            DeviceInfoText!.Text =  GetString(Resource.String.status_no_port);
            StatusText!.Text = GetString(Resource.String.status_error_loading);
        }
    }

    async System.Threading.Tasks.Task ConnectToDevice()
    {
        if ((usbManager == null) || (portInfo == null))
            return;

        try
        {
            StatusText!.Text = "Connecting...";
            ConnectButton!.Enabled = false;

            // Find the driver for this device
            var drivers = await MainActivity.FindAllDriversAsync(usbManager);
            IUsbSerialDriver? driver = null;

            foreach (var d in drivers)
            {
                if (d.Device.VendorId == portInfo.VendorId && d.Device.DeviceId == portInfo.DeviceId)
                {
                    driver = d;
                    break;
                }
            }

            if (driver == null)
            {
                StatusText.Text = "Driver not found for this device";
                return;
            }

            var port = driver.Ports[portInfo.PortNumber];
            if (port == null)
            {
                StatusText.Text = "Port not found";
                return;
            }

            // Test basic connection
            Log.Info(TAG, $"Attempting to connect to {port}");

            if (OpenSerialPort(port))
            {
                // For demo purposes, we'll just show success
                StatusText.Text = "Connection successful!\n" +
                                $"Port: {port.GetType().Name}\n" +
                                $"Driver: {driver.GetType().Name}\n";

                Log.Info(TAG, "Connection test completed successfully");
            }
            Log.Info(TAG, "Connection test failed");


        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Connection error: {ex.Message}");
            StatusText!.Text = $"Connection failed: {ex.Message}";
        }
        finally
        {
            ConnectButton!.Enabled = true;
        }
    }

    bool OpenSerialPort(UsbSerialPort port)
    {
        // Replace these settings with those required by your device
        serialIoManager = new SerialInputOutputManager(port)
        {
            BaudRate = 115200,
            DataBits = 8,
            StopBits = StopBits.One,
            Parity = Parity.None,
        };

        serialIoManager.DataReceived += (sender, e) => {
            RunOnUiThread(() => {
                UpdateReceivedData(e.Data);
            });
        };
        serialIoManager.ErrorReceived += (sender, e) => {
            RunOnUiThread(() => {
                StatusText!.Append($"Error: {e}\n");
            });
        };

        Log.Info(TAG, "Starting IO manager ..");

        try
        {
            serialIoManager.Open(usbManager);
        }
        catch (Java.IO.IOException e)
        {
            StatusText!.Append($"Error opening device: {e.Message}\n");
            return false;
        }

        return true;

    }

    void WriteData(byte[] data, UsbSerialPort port)
    {
        if (serialIoManager!.IsOpen)
        {
            port.Write(data, WRITE_WAIT_MILLIS);
        }
    }

    void UpdateReceivedData(byte[] data)
    {
        var message = "Read " + data.Length + " bytes: \n"
            + HexDump.ByteArrayToHexString(data) + "\n\n";

        if (StatusText != null)
        {
            StatusText.Append(message);
            StatusScrollView!.Post(() => StatusScrollView.ScrollTo(0, StatusText.Bottom));
        }
    }

}