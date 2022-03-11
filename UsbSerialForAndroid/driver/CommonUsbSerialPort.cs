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
using Java.Util.Jar;

namespace Hoho.Android.UsbSerial.Driver
{
    public abstract class CommonUsbSerialPort : UsbSerialPort
    {
        public static int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;
        public static int DEFAULT_WRITE_BUFFER_SIZE = 16 * 1024;

        protected UsbDevice mDevice;
        protected int mPortNumber;

        // non-null when open()
        protected UsbDeviceConnection mConnection = null;

        // check if connection is still available
        public bool HasConnection => mConnection != null;

        protected object mReadBufferLock = new object();
        protected object mWriteBufferLock = new object();

        /** Internal read buffer.  Guarded by {@link #mReadBufferLock}. */
        protected byte[] mReadBuffer;

        /** Internal write buffer.  Guarded by {@link #mWriteBufferLock}. */
        protected byte[] mWriteBuffer;

        public CommonUsbSerialPort(UsbDevice device, int portNumber)
        {
            mDevice = device;
            mPortNumber = portNumber;

            mReadBuffer = new byte[DEFAULT_READ_BUFFER_SIZE];
            mWriteBuffer = new byte[DEFAULT_WRITE_BUFFER_SIZE];
        }
        public override string ToString()
        {
            return
                $"<{this.GetType().Name} device_name={mDevice.DeviceName} device_id={mDevice.DeviceId} port_number={mPortNumber}>";
        }

        /**
            * Returns the currently-bound USB device.
            *
            * @return the device
            */
        public UsbDevice GetDevice()
        {
            return mDevice;
        }

        public override int GetPortNumber()
        {
            return mPortNumber;
        }

        /**
         * Returns the device serial number
         *  @return serial number
         */
        public override string GetSerial()
        {
            return mConnection.Serial;
        }

        /**
         * Sets the size of the internal buffer used to exchange data with the USB
         * stack for read operations.  Most users should not need to change this.
         *
         * @param bufferSize the size in bytes
         */
        public void SetReadBufferSize(int bufferSize)
        {
            lock(mReadBufferLock) {
                if (bufferSize == mReadBuffer.Length)
                {
                    return;
                }
                mReadBuffer = new byte[bufferSize];
            }
        }

        /**
         * Sets the size of the internal buffer used to exchange data with the USB
         * stack for write operations.  Most users should not need to change this.
         *
         * @param bufferSize the size in bytes
         */
        public void SetWriteBufferSize(int bufferSize)
        {
            lock(mWriteBufferLock) {
                if (bufferSize == mWriteBuffer.Length)
                {
                    return;
                }
                mWriteBuffer = new byte[bufferSize];
            }
        }

        public abstract override void Open(UsbDeviceConnection connection);

        public abstract override void Close();

        public abstract override int Read(byte[] dest, int timeoutMillis);

        public abstract override int Write(byte[] src, int timeoutMillis);

        public abstract override void SetParameters(
            int baudRate, int dataBits, StopBits stopBits, Parity parity);

        public abstract override bool GetCD();

        public abstract override bool GetCTS();

        public abstract override bool GetDSR();

        public abstract override bool GetDTR();

        public abstract override void SetDTR(bool value);

        public abstract override bool GetRI();

        public abstract override bool GetRTS();

        public abstract override void SetRTS(bool value);

        public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers)
        {
            return !flushReadBuffers && !flushWriteBuffers;
        }
    }
}