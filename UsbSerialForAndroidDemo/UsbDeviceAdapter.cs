using Android.Content;
using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace UsbSerialForAndroidDemo
{
    public class UsbDeviceAdapter(List<UsbSerialPort> ports, Func<UsbSerialPort, Task> onItemClick, Context? context) : RecyclerView.Adapter
    {
        readonly List<UsbSerialPort> ports = ports;
        readonly Func<UsbSerialPort, Task> onItemClick = onItemClick;
        readonly Context? context = context;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!
                .Inflate(Resource.Layout.usb_device_row, parent, false);

            return new UsbDeviceViewHolder(view!, onItemClick, context);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var viewHolder = holder as UsbDeviceViewHolder;
            viewHolder?.Bind(ports[position]);
        }

        public override int ItemCount => ports.Count;
    }
}