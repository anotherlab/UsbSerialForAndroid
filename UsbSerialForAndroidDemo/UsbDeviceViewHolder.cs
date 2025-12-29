using Android.Content;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Hoho.Android.UsbSerial.Driver;

namespace UsbSerialForAndroidDemo
{
    public class UsbDeviceViewHolder : RecyclerView.ViewHolder
    {
        public TextView? ProductName { get; }
        public TextView? ProductId { get; }
        public TextView? DeviceName { get; }

        UsbSerialPort? currentPort;
        readonly Func<UsbSerialPort, Task> onItemClick;
        readonly Context? context;

        public UsbDeviceViewHolder(View itemView, Func<UsbSerialPort, Task> onItemClick, Context? context) : base(itemView)
        {
            ProductName = itemView.FindViewById<TextView>(Resource.Id.productname);
            ProductId = itemView.FindViewById<TextView>(Resource.Id.productid);
            DeviceName = itemView.FindViewById<TextView>(Resource.Id.devicename);
            this.context = context;

            this.onItemClick = onItemClick;

            itemView.Click += async (sender, e) =>
            {
                if (currentPort != null)
                    await onItemClick(currentPort);
            };
        }

        public void Bind(UsbSerialPort port)
        {
            currentPort = port;
            var device = port.Driver.Device;

            ProductName!.Text = device.ProductName ?? context!.GetString(Resource.String.unknown_product);
            ProductId!.Text = $"Vendor {device.VendorId:X4} Product {device.ProductId:X4}";
            DeviceName!.Text = $"{device.Class.SimpleName} - {device.DeviceName}";
        }
    }
}