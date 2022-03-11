/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System.Collections.Generic;

using Android.Hardware.Usb;

namespace Hoho.Android.UsbSerial.Driver
{
    public interface IUsbSerialDriver
    {
        UsbDevice Device { get; }

        UsbDevice GetDevice();

        List<UsbSerialPort> Ports { get; }
        List<UsbSerialPort> GetPorts();

        //Dictionary<int, int[]> GetSupportedDevices();
    }
    public class UsbSerialDriver : IUsbSerialDriver
    {
        protected UsbDevice mDevice;
        protected UsbSerialPort mPort;
        public UsbDevice Device => GetDevice();

        public List<UsbSerialPort> Ports => GetPorts();

        public UsbDevice GetDevice()
        {
            return mDevice;
        }

        public virtual List<UsbSerialPort> GetPorts()
        {
            return new List<UsbSerialPort> { mPort };
        }

    }
}