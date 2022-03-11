/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

namespace Hoho.Android.UsbSerial.Driver
{
    public class Cp21xxSerialDriver : UsbSerialDriver
    {
        private readonly string TAG = typeof(Cp21xxSerialDriver).Name;

        public Cp21xxSerialDriver(UsbDevice device)
        {
            mDevice = device;
            mPort = new Cp21xxSerialPort(mDevice, 0, this);
        }

        public class Cp21xxSerialPort : CommonUsbSerialPort
        {
            private const int DEFAULT_BAUD_RATE = 9600;

            private const int USB_WRITE_TIMEOUT_MILLIS = 5000;

            /*
             * Configuration Request Types
             */
            private const int REQTYPE_HOST_TO_DEVICE = 0x41;

            /*
             * Configuration Request Codes
             */
            private const int SILABSER_IFC_ENABLE_REQUEST_CODE = 0x00;
            private const int SILABSER_SET_BAUDDIV_REQUEST_CODE = 0x01;
            private const int SILABSER_SET_LINE_CTL_REQUEST_CODE = 0x03;
            private const int SILABSER_SET_MHS_REQUEST_CODE = 0x07;
            private const int SILABSER_SET_BAUDRATE = 0x1E;
            private const int SILABSER_FLUSH_REQUEST_CODE = 0x12;

            private const int FLUSH_READ_CODE = 0x0a;
            private const int FLUSH_WRITE_CODE = 0x05;

            /*
             * SILABSER_IFC_ENABLE_REQUEST_CODE
             */
            private const int UART_ENABLE = 0x0001;
            private const int UART_DISABLE = 0x0000;

            /*
             * SILABSER_SET_BAUDDIV_REQUEST_CODE
             */
            private const int BAUD_RATE_GEN_FREQ = 0x384000;

            /*
             * SILABSER_SET_MHS_REQUEST_CODE
             */
            private const int MCR_DTR = 0x0001;
            private const int MCR_RTS = 0x0002;
            private const int MCR_ALL = 0x0003;

            private const int CONTROL_WRITE_DTR = 0x0100;
            private const int CONTROL_WRITE_RTS = 0x0200;

            private UsbEndpoint mReadEndpoint;
            private UsbEndpoint mWriteEndpoint;

            private IUsbSerialDriver Driver;
            private string TAG => (Driver as Cp21xxSerialDriver)?.TAG;



            public Cp21xxSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
            {
                Driver = driver;
            }

            public override IUsbSerialDriver GetDriver()
            {
                return Driver;
            }

            private int SetConfigSingle(int request, int value)
            {
                return mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request, value,
                        0, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            }

            public override void Open(UsbDeviceConnection connection)
            {
                if (mConnection != null)
                {
                    throw new IOException("Already opened.");
                }

                mConnection = connection;
                Boolean opened = false;
                try
                {
                    for (int i = 0; i < mDevice.InterfaceCount; i++)
                    {
                        UsbInterface usbIface = mDevice.GetInterface(i);
                        if (mConnection.ClaimInterface(usbIface, true))
                        {
                            Log.Debug(TAG, $"claimInterface {i} SUCCESS");
                        }
                        else
                        {
                            Log.Debug(TAG, $"claimInterface {i} FAIL");
                        }
                    }

                    UsbInterface dataIface = mDevice.GetInterface(mDevice.InterfaceCount - 1);
                    for (int i = 0; i < dataIface.EndpointCount; i++)
                    {
                        UsbEndpoint ep = dataIface.GetEndpoint(i);
                        if (ep.Type == (UsbAddressing)UsbSupport.UsbEndpointXferBulk)
                        {
                            if (ep.Direction == (UsbAddressing)UsbSupport.UsbDirIn)
                            {
                                mReadEndpoint = ep;
                            }
                            else
                            {
                                mWriteEndpoint = ep;
                            }
                        }
                    }

                    SetConfigSingle(SILABSER_IFC_ENABLE_REQUEST_CODE, UART_ENABLE);
                    SetConfigSingle(SILABSER_SET_MHS_REQUEST_CODE, MCR_ALL | CONTROL_WRITE_DTR | CONTROL_WRITE_RTS);
                    SetConfigSingle(SILABSER_SET_BAUDDIV_REQUEST_CODE, BAUD_RATE_GEN_FREQ / DEFAULT_BAUD_RATE);
                    //            setParameters(DEFAULT_BAUD_RATE, DEFAULT_DATA_BITS, DEFAULT_STOP_BITS, DEFAULT_PARITY);
                    opened = true;
                }
                finally
                {
                    if (!opened)
                    {
                        try
                        {
                            Close();
                        }
                        catch (IOException e)
                        {
                            // Ignore IOExceptions during close()
                        }
                    }
                }
            }

            public override void Close()
            {
                if (mConnection == null)
                {
                    throw new IOException("Already closed");
                }
                try
                {
                    SetConfigSingle(SILABSER_IFC_ENABLE_REQUEST_CODE, UART_DISABLE);
                    mConnection.Close();
                }
                finally
                {
                    mConnection = null;
                }
            }

            public override int Read(byte[] dest, int timeoutMillis)
            {
                int numBytesRead;
                lock(mReadBufferLock) {
                    int readAmt = Math.Min(dest.Length, mReadBuffer.Length);
                    numBytesRead = mConnection.BulkTransfer(mReadEndpoint, mReadBuffer, readAmt,
                            timeoutMillis);
                    if (numBytesRead < 0)
                    {
                        // This sucks: we get -1 on timeout, not 0 as preferred.
                        // We *should* use UsbRequest, except it has a bug/api oversight
                        // where there is no way to determine the number of bytes read
                        // in response :\ -- http://b.android.com/28023
                        return 0;
                    }
                    Buffer.BlockCopy(mReadBuffer, 0, dest, 0, numBytesRead);
                }
                return numBytesRead;
            }

            public override int Write(byte[] src, int timeoutMillis)
            {
                int offset = 0;

                while (offset < src.Length)
                {
                    int writeLength;
                    int amtWritten;

                    lock(mWriteBufferLock) {
                        byte[] writeBuffer;

                        writeLength = Math.Min(src.Length - offset, mWriteBuffer.Length);
                        if (offset == 0)
                        {
                            writeBuffer = src;
                        }
                        else
                        {
                            // bulkTransfer does not support offsets, make a copy.
                            Buffer.BlockCopy(src, offset, mWriteBuffer, 0, writeLength);
                            writeBuffer = mWriteBuffer;
                        }

                        amtWritten = mConnection.BulkTransfer(mWriteEndpoint, writeBuffer, writeLength,
                                timeoutMillis);
                    }
                    if (amtWritten <= 0)
                    {
                        throw new IOException(
                            $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                    }

                    Log.Debug(TAG, $"Wrote amt={amtWritten} attempted={writeLength}");
                    offset += amtWritten;
                }
                return offset;
            }

            private void SetBaudRate(int baudRate)
            {
                byte[] data = new byte[] {
                    (byte) ( baudRate & 0xff),
                    (byte) ((baudRate >> 8 ) & 0xff),
                    (byte) ((baudRate >> 16) & 0xff),
                    (byte) ((baudRate >> 24) & 0xff)
                };
                int ret = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SILABSER_SET_BAUDRATE,
                        0, 0, data, 4, USB_WRITE_TIMEOUT_MILLIS);
                if (ret < 0)
                {
                    throw new IOException("Error setting baud rate.");
                }
            }


            public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
            {
                SetBaudRate(baudRate);

                int configDataBits = 0;
                switch (dataBits)
                {
                    case DATABITS_5:
                        configDataBits |= 0x0500;
                        break;
                    case DATABITS_6:
                        configDataBits |= 0x0600;
                        break;
                    case DATABITS_7:
                        configDataBits |= 0x0700;
                        break;
                    case DATABITS_8:
                        configDataBits |= 0x0800;
                        break;
                    default:
                        configDataBits |= 0x0800;
                        break;
                }

                switch (parity)
                {
                    case Parity.Odd:
                        configDataBits |= 0x0010;
                        break;
                    case Parity.Even:
                        configDataBits |= 0x0020;
                        break;
                }

                switch (stopBits)
                {
                    case StopBits.One:
                        configDataBits |= 0;
                        break;
                    case StopBits.Two:
                        configDataBits |= 2;
                        break;
                }
                SetConfigSingle(SILABSER_SET_LINE_CTL_REQUEST_CODE, configDataBits);
            }

            public override bool GetCD()
            {
                return false;
            }

            public override bool GetCTS()
            {
                return false;
            }

            public override bool GetDSR()
            {
                return false;
            }

            public override bool GetDTR()
            {
                return true;
            }

            public override void SetDTR(bool value)
            {
            }

            public override bool GetRI()
            {
                return false;
            }

            public override bool GetRTS()
            {
                return true;
            }

            public override void SetRTS(bool value)
            {
            }

            public override Boolean PurgeHwBuffers(Boolean purgeReadBuffers, Boolean purgeWriteBuffers)
            {
                int value = (purgeReadBuffers ? FLUSH_READ_CODE : 0)
                        | (purgeWriteBuffers ? FLUSH_WRITE_CODE : 0);

                if (value != 0)
                {
                    SetConfigSingle(SILABSER_FLUSH_REQUEST_CODE, value);
                }

                return true;
            }
        }

        public static Dictionary<int, int[]> GetSupportedDevices()
        {
            return new Dictionary<int, int[]>
            {
                {
                    UsbId.VENDOR_SILABS, new int[]
                    {
                        UsbId.SILABS_CP2102,
                        UsbId.SILABS_CP2105,
                        UsbId.SILABS_CP2108,
                        UsbId.SILABS_CP2110
                    }
                }
            };
        }
    }
}