using Android.Content;
using Android.Hardware.Usb;
using Android.Util;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Extensions;

[assembly: UsesFeature("android.hardware.usb.host")]

namespace UsbSerialForAndroidDemo
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    [IntentFilter([UsbManager.ActionUsbDeviceAttached])]
    [MetaData(UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
    public class MainActivity : Activity
    {
        static readonly string TAG = typeof(MainActivity).Name;
        const string ACTION_USB_PERMISSION = "com.hoho.android.usbserial.demo.USB_PERMISSION";

        UsbManager? usbManager;
        RecyclerView? recyclerView;
        TextView? statusText;
        ProgressBar? progressBar;

        UsbDeviceAdapter? adapter;
        BroadcastReceiver? detachedReceiver;
        readonly List<UsbSerialPort> usbPorts = [];
        bool receiverInitalized = false;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            var rootView = FindViewById<Android.Views.ViewGroup>(Android.Resource.Id.Content);
            if (rootView != null)
            {
                var statusBarHeight = GetStatusBarHeight();
                rootView.SetPadding(0, statusBarHeight, 0, 0);
            }

            usbManager = GetSystemService(Context.UsbService) as UsbManager;
            recyclerView = FindViewById<RecyclerView>(Resource.Id.recyclerView);
            statusText = FindViewById<TextView>(Resource.Id.statusText);
            progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);

            // Setup RecyclerView
            recyclerView!.SetLayoutManager(new LinearLayoutManager(this));
            adapter = new UsbDeviceAdapter(usbPorts, OnItemClick, this);
            recyclerView.SetAdapter(adapter);
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


        protected override async void OnResume()
        {
            base.OnResume();

            await PopulateDeviceListAsync();

            // Register the broadcast receiver
            detachedReceiver = new UsbDeviceDetachedReceiver(this);
            RegisterReceiver(detachedReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));
            receiverInitalized = true;
        }

        protected override void OnPause()
        {
            base.OnPause();

            if (!receiverInitalized)
                return;

            // Unregister the broadcast receiver
            var temp = detachedReceiver;
            if (temp != null)
                UnregisterReceiver(temp);

            receiverInitalized = false;
        }

        internal static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(UsbManager usbManager)
        {
            var table = UsbSerialProber.DefaultProbeTable;

            // Add custom drivers here if needed
            //table.AddProduct(0x1b4f, 0x0008, typeof(CdcAcmSerialDriver)); // IOIO OTG
            //table.AddProduct(0x09D8, 0x0420, typeof(CdcAcmSerialDriver)); // Elatec TWN4

            var prober = new UsbSerialProber(table);
            return prober.FindAllDriversAsync(usbManager);
        }

        async Task OnItemClick(UsbSerialPort port)
        {
            Log.Info(TAG, $"Selected USB device: {port.Driver.Device.DeviceName}");

            // Request user permission to connect to device
            if (usbManager != null)
            {
                var permissionGranted = await usbManager.RequestPermissionAsync(port.Driver.Device, this);
                if (permissionGranted)
                {
                    // Start the UsbDeviceActivity for this device
                    var intent = new Intent(this, typeof(UsbDeviceActivity));
                    intent.PutExtra(UsbDeviceActivity.EXTRA_USB_DEVICE, new UsbSerialPortInfo(port));
                    StartActivity(intent);
                }
                else
                {
                    Toast.MakeText(this, GetString(Resource.String.toast_permission_denied), ToastLength.Short)!.Show();
                }
            }
            else
            {
                Toast.MakeText(this, GetString(Resource.String.toast_usb_manager_unavailable), ToastLength.Short)!.Show();
            }
        }

        internal async Task PopulateDeviceListAsync()
        {
            ShowProgressBar();

            Log.Info(TAG, "Refreshing device list...");

            try
            {
                if (usbManager != null)
                {
                    var drivers = await FindAllDriversAsync(usbManager);

                    usbPorts.Clear();
                    foreach (var driver in drivers)
                    {
                        var ports = driver.Ports;
                        Log.Info(TAG, $"+ {driver}: {ports.Count} port{(ports.Count == 1 ? "" : "s")}");
                        foreach (var port in ports)
                        {
                            usbPorts.Add(port);
                        }
                    }

                    RunOnUiThread(() =>
                    {
                        adapter!.NotifyDataSetChanged();
                        // Use plurals resource for device count
                        statusText!.Text = Resources!.GetQuantityString(Resource.Plurals.devices_found, usbPorts.Count, usbPorts.Count);
                        statusText!.Text += $"\n{GetString(Resource.String.tap_to_connect)}";
                    });

                    Log.Info(TAG, $"Done refreshing, {usbPorts.Count} entries found.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Error populating device list: {ex.Message}");
                RunOnUiThread(() =>
                {
                    statusText!.Text = GetString(Resource.String.status_error_loading_devices);
                });
            }
            finally
            {
                HideProgressBar();
            }
        }

        void ShowProgressBar()
        {
            RunOnUiThread(() =>
            {
                progressBar!.Visibility = ViewStates.Visible;
                statusText!.Text = GetString(Resource.String.status_refreshing);
            });
        }

        void HideProgressBar()
        {
            RunOnUiThread(() =>
            {
                progressBar!.Visibility = ViewStates.Gone;
            });
        }
    }
}