/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Java.Lang.Reflect;

namespace Hoho.Android.UsbSerial.Driver
{
    public class UsbSerialProber
    {
        private ProbeTable mProbeTable;

        public UsbSerialProber(ProbeTable probeTable)
        {
            mProbeTable = probeTable;
        }

        public static UsbSerialProber GetDefaultProber()
        {
            return new UsbSerialProber(GetDefaultProbeTable());
        }

        public static ProbeTable DefaultProbeTable => GetDefaultProbeTable();

        public static ProbeTable GetDefaultProbeTable()
        {
            ProbeTable probeTable = new ProbeTable();
            probeTable.AddDriver(typeof(CdcAcmSerialDriver));
            probeTable.AddDriver(typeof(Cp21xxSerialDriver));
            probeTable.AddDriver(typeof(FtdiSerialDriver));
            probeTable.AddDriver(typeof(ProlificSerialDriver));
            probeTable.AddDriver(typeof(Ch34xSerialDriver));
            return probeTable;
        }

        /**
         * Finds and builds all possible {@link UsbSerialDriver UsbSerialDrivers}
         * from the currently-attached {@link UsbDevice} hierarchy. This method does
         * not require permission from the Android USB system, since it does not
         * open any of the devices.
         *
         * @param usbManager
         * @return a list, possibly empty, of all compatible drivers
         */
        public List<IUsbSerialDriver> FindAllDrivers(UsbManager usbManager)
        {
            List< IUsbSerialDriver > result = new List<IUsbSerialDriver>();

            foreach (UsbDevice usbDevice in usbManager.DeviceList.Values)
            {
                IUsbSerialDriver driver = ProbeDevice(usbDevice);
                if (driver != null)
                {
                    result.Add(driver);
                }
            }
            return result;
        }

        /**
         * Probes a single device for a compatible driver.
         * 
         * @param usbDevice the usb device to probe
         * @return a new {@link UsbSerialDriver} compatible with this device, or
         *         {@code null} if none available.
         */
        public IUsbSerialDriver ProbeDevice(UsbDevice usbDevice)
        {
            int vendorId = usbDevice.VendorId;
            int productId = usbDevice.ProductId;

            var driverClass = mProbeTable.FindDriver(vendorId, productId);
            if (driverClass != null)
            {
                IUsbSerialDriver driver;
                try
                {
                    driver = (IUsbSerialDriver)Activator.CreateInstance(driverClass, new System.Object[] {usbDevice});
                } catch (NoSuchMethodException e) {
                    throw new RuntimeException(e);
                } catch (IllegalArgumentException e) {
                    throw new RuntimeException(e);
                } catch (InstantiationException e) {
                    throw new RuntimeException(e);
                } catch (IllegalAccessException e) {
                    throw new RuntimeException(e);
                } catch (InvocationTargetException e) {
                    throw new RuntimeException(e);
                }
                return driver;
            }
            return null;
        }

    }
}