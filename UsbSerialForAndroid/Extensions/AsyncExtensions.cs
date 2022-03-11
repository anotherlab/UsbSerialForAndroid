/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Hardware.Usb;

using Hoho.Android.UsbSerial.Driver;

namespace Hoho.Android.UsbSerial.Extensions
{
    public static partial class AsyncExtensions
    {
        public static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(this UsbSerialProber prober, UsbManager manager)
        {
            var tcs = new TaskCompletionSource<IList<IUsbSerialDriver>>();

            Task.Run(() =>
            {
                tcs.TrySetResult(prober.FindAllDrivers(manager));
            });
            return tcs.Task;
        }
    }
}