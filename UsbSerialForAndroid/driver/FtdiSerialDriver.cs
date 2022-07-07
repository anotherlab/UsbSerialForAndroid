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
using System.IO;
using Android.Util;
using Hoho.Android.UsbSerial.Util;
using Java.Lang;
using Boolean = System.Boolean;
using Math = System.Math;
using String = System.String;
using Hoho.Android.UsbSerial.Extensions;

/*
 * driver is implemented from various information scattered over FTDI documentation
 *
 * baud rate calculation https://www.ftdichip.com/Support/Documents/AppNotes/AN232B-05_BaudRates.pdf
 * control bits https://www.ftdichip.com/Firmware/Precompiled/UM_VinculumFirmware_V205.pdf
 * device type https://www.ftdichip.com/Support/Documents/AppNotes/AN_233_Java_D2XX_for_Android_API_User_Manual.pdf -> bvdDevice
 *
 */

namespace Hoho.Android.UsbSerial.Driver
{
    public class FtdiSerialDriver : UsbSerialDriver
    {
        private List<UsbSerialPort> mPorts;
        private enum DeviceType
        {
            TYPE_BM,
            TYPE_AM,
            TYPE_2232C,
            TYPE_R,
            TYPE_2232H,
            TYPE_4232H
        }

        public FtdiSerialDriver(UsbDevice device)
        {
            mDevice = device;
            mPort = new FtdiSerialPort(mDevice, 0, this);

            mPorts = new List<UsbSerialPort>();

            for (int port = 0; port < device.InterfaceCount; port++)
            {
                mPorts.Add(new FtdiSerialPort(mDevice, port, this));
            }
        }

        // Needs to refactored
        public override List<UsbSerialPort> GetPorts()
        {
            return mPorts;
        }

        private class FtdiSerialPort : CommonUsbSerialPort
        {
            private static int USB_WRITE_TIMEOUT_MILLIS = 5000;
            private static int READ_HEADER_LENGTH = 2; // contains MODEM_STATUS

            // https://developer.android.com/reference/android/hardware/usb/UsbConstants#USB_DIR_IN
            private static int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | 128; // UsbConstants.USB_DIR_OUT;
            private static int REQTYPE_DEVICE_TO_HOST = UsbConstants.UsbTypeVendor | 0;   // UsbConstants.USB_DIR_IN;

            private static int RESET_REQUEST = 0;
            private static int MODEM_CONTROL_REQUEST = 1;
            private static int SET_BAUD_RATE_REQUEST = 3;
            private static int SET_DATA_REQUEST = 4;
            private static int GET_MODEM_STATUS_REQUEST = 5;
            private static int SET_LATENCY_TIMER_REQUEST = 9;
            private static int GET_LATENCY_TIMER_REQUEST = 10;

            private static int MODEM_CONTROL_DTR_ENABLE = 0x0101;
            private static int MODEM_CONTROL_DTR_DISABLE = 0x0100;
            private static int MODEM_CONTROL_RTS_ENABLE = 0x0202;
            private static int MODEM_CONTROL_RTS_DISABLE = 0x0200;
            private static int MODEM_STATUS_CTS = 0x10;
            private static int MODEM_STATUS_DSR = 0x20;
            private static int MODEM_STATUS_RI = 0x40;
            private static int MODEM_STATUS_CD = 0x80;
            private static int RESET_ALL = 0;
            private static int RESET_PURGE_RX = 1;
            private static int RESET_PURGE_TX = 2;

            private Boolean baudRateWithPort = false;
            private Boolean dtr = false;
            private Boolean rts = false;
            private int breakConfig = 0;

            private IUsbSerialDriver Driver;


            private String TAG = typeof (FtdiSerialDriver).Name;


            public FtdiSerialPort(UsbDevice device, int portNumber) : base(device, portNumber)
            {
            }

            public FtdiSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
            {
                this.Driver = driver;
            }

            public override IUsbSerialDriver GetDriver()
            {
                return Driver;
            }

            public void Reset()
            {
                int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                    RESET_ALL, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new IOException("Reset failed: result=" + result);
                }
            }

            public override void Open(UsbDeviceConnection connection)
            {
                if (mConnection != null) {
                    throw new IOException("Already open");
                }
                mConnection = connection;

                Boolean opened = false;
                try {
                    for (int i = 0; i < mDevice.InterfaceCount; i++)
                    {
                        if (connection.ClaimInterface(mDevice.GetInterface(i), true))
                        {
                            Log.Debug(TAG, "claimInterface " + i + " SUCCESS");
                        }
                        else
                        {
                            throw new IOException("Error claiming interface " + i);
                        }
                    }
                    Reset();
                    opened = true;
                } finally {
                    if (!opened)
                    {
                        Close();
                        mConnection = null;
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
                    mConnection.Close();
                }
                finally
                {
                    mConnection = null;
                }
            }

            public override int Read(byte[] dest, int timeoutMillis)
            {
                UsbEndpoint endpoint = mDevice.GetInterface(0).GetEndpoint(0);

                int totalBytesRead;

                lock(mReadBufferLock) {
                    int readAmt = Math.Min(dest.Length, mReadBuffer.Length);

                    // todo: replace with async call
                    totalBytesRead = mConnection.BulkTransfer(endpoint, mReadBuffer,
                            readAmt, timeoutMillis);

                    if (totalBytesRead < READ_HEADER_LENGTH)
                    {
                        throw new IOException("Expected at least " + READ_HEADER_LENGTH + " bytes");
                    }

                    return ReadFilter(dest, totalBytesRead, endpoint.MaxPacketSize);
                }
            }

            protected int ReadFilter(byte[] buffer, int totalBytesRead, int maxPacketSize)
            {
                int destPos = 0;

                for (int srcPos = 0; srcPos < totalBytesRead; srcPos += maxPacketSize)
                {
                    int length = Math.Min(srcPos + maxPacketSize, totalBytesRead) - (srcPos + READ_HEADER_LENGTH);
                    if (length < 0)
                        throw new IOException("Expected at least " + READ_HEADER_LENGTH + " bytes");

                    Buffer.BlockCopy(mReadBuffer, srcPos + READ_HEADER_LENGTH, buffer, destPos, length);
                    destPos += length;
                }
                return destPos;
            }

            public override int Write(byte[] src, int timeoutMillis)
            {
                UsbEndpoint endpoint = mDevice.GetInterface(0).GetEndpoint(1);
                int offset = 0;

                while (offset < src.Length)
                {
                    int writeLength;
                    int amtWritten;

                    lock (mWriteBufferLock)
                    {
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

                        amtWritten = mConnection.BulkTransfer(endpoint, writeBuffer, writeLength,
                                timeoutMillis);
                    }

                    if (amtWritten <= 0)
                    {
                        throw new IOException("Error writing " + writeLength
                                + " bytes at offset " + offset + " length=" + src.Length);
                    }

                    Log.Debug(TAG, "Wrote amtWritten=" + amtWritten + " attempted=" + writeLength);
                    offset += amtWritten;
                }
                return offset;
            }


            private int SetBaudRate(int baudRate)
            {
                int divisor, subdivisor, effectiveBaudRate;

                if (baudRate > 3500000)
                {
                    throw new UnsupportedOperationException("Baud rate to high");
                }
                else if (baudRate >= 2500000)
                {
                    divisor = 0;
                    subdivisor = 0;
                    effectiveBaudRate = 3000000;
                }
                else if (baudRate >= 1750000)
                {
                    divisor = 1;
                    subdivisor = 0;
                    effectiveBaudRate = 2000000;
                }
                else
                {
                    divisor = (24000000 << 1) / baudRate;
                    divisor = (divisor + 1) >> 1; // round
                    subdivisor = divisor & 0x07;
                    divisor >>= 3;
                    if (divisor > 0x3fff) // exceeds bit 13 at 183 baud
                        throw new UnsupportedOperationException("Baud rate to low");
                    effectiveBaudRate = (24000000 << 1) / ((divisor << 3) + subdivisor);
                    effectiveBaudRate = (effectiveBaudRate + 1) >> 1;
                }
                double baudRateError = Math.Abs(1.0 - (effectiveBaudRate / (double)baudRate));
                if (baudRateError >= 0.031) // can happen only > 1.5Mbaud
                    throw new UnsupportedOperationException(String.Format("Baud rate deviation %.1f%% is higher than allowed 3%%", baudRateError * 100));
                int value = divisor;
                int index = 0;
                switch (subdivisor)
                {
                    case 0: break; // 16,15,14 = 000 - sub-integer divisor = 0
                    case 4: value |= 0x4000; break; // 16,15,14 = 001 - sub-integer divisor = 0.5
                    case 2: value |= 0x8000; break; // 16,15,14 = 010 - sub-integer divisor = 0.25
                    case 1: value |= 0xc000; break; // 16,15,14 = 011 - sub-integer divisor = 0.125
                    case 3: value |= 0x0000; index |= 1; break; // 16,15,14 = 100 - sub-integer divisor = 0.375
                    case 5: value |= 0x4000; index |= 1; break; // 16,15,14 = 101 - sub-integer divisor = 0.625
                    case 6: value |= 0x8000; index |= 1; break; // 16,15,14 = 110 - sub-integer divisor = 0.75
                    case 7: value |= 0xc000; index |= 1; break; // 16,15,14 = 111 - sub-integer divisor = 0.875
                }
                if (baudRateWithPort)
                {
                    index <<= 8;
                    index |= mPortNumber + 1;
                }
                int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SET_BAUD_RATE_REQUEST,
                        value, index, null, 0, USB_WRITE_TIMEOUT_MILLIS);

                if (result != 0)
                {
                    throw new IOException("Setting baudrate failed: result=" + result);
                }

                return effectiveBaudRate;
            }


            public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
            {
                if (baudRate <= 0)
                {
                    throw new IllegalArgumentException("Invalid baud rate: " + baudRate);
                }

                SetBaudRate(baudRate);

                int config = dataBits;

                switch (dataBits)
                {
                    case DATABITS_5:
                    case DATABITS_6:
                        throw new UnsupportedOperationException("Unsupported data bits: " + dataBits);
                    case DATABITS_7:
                    case DATABITS_8:
                        config |= dataBits;
                        break;
                    default:
                        throw new IllegalArgumentException("Invalid data bits: " + dataBits);
                }

                switch (parity)
                {
                    case Parity.None:
                        break;
                    case Parity.Odd:
                        config |= 0x100;
                        break;
                    case Parity.Even:
                        config |= 0x200;
                        break;
                    case Parity.Mark:
                        config |= 0x300;
                        break;
                    case Parity.Space:
                        config |= 0x400;
                        break;
                    default:
                        throw new IllegalArgumentException("Unknown parity value: " + parity);
                }

                switch (stopBits)
                {
                    case StopBits.One:
                        break;
                    case StopBits.OnePointFive:
                        throw new UnsupportedOperationException("Unsupported stop bits: 1.5");
                    case StopBits.Two:
                        config |= 0x1000;
                        break;
                    default:
                        throw new IllegalArgumentException("Unknown stopBits value: " + stopBits);
                }

                int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SET_DATA_REQUEST,
                        config, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);

                if (result != 0)
                {
                    throw new IOException("Setting parameters failed: result=" + result);
                }
                breakConfig = config;
            }

            private int GetStatus()
            {
                byte[] data = new byte[2];
                int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_DEVICE_TO_HOST, GET_MODEM_STATUS_REQUEST,
                        0, mPortNumber + 1, data, data.Length, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 2) {
                    throw new IOException("Get modem status failed: result=" + result);
                }
                return data[0];
            }

            public override Boolean GetCD()
            {
                return (GetStatus() & MODEM_STATUS_CD) != 0;
            }

            public override Boolean GetCTS()
            {
                return (GetStatus() & MODEM_STATUS_CTS) != 0;
            }

            public override Boolean GetDSR()
            {
                return (GetStatus() & MODEM_STATUS_DSR) != 0;
            }

            public override Boolean GetDTR()
            {
                return dtr;
            }

            public override void SetDTR(Boolean value)
            {
                int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, MODEM_CONTROL_REQUEST,
                                    value ? MODEM_CONTROL_DTR_ENABLE : MODEM_CONTROL_DTR_DISABLE, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new IOException("Set DTR failed: result=" + result);
                }
                dtr = value;
            }

            public override Boolean GetRI()
            {
                return (GetStatus() & MODEM_STATUS_RI) != 0;
            }

            public override Boolean GetRTS()
            {
                return rts;
            }

            public override void SetRTS(Boolean value)
            {
                int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, MODEM_CONTROL_REQUEST,
                        value ? MODEM_CONTROL_RTS_ENABLE : MODEM_CONTROL_RTS_DISABLE, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new IOException("Set DTR failed: result=" + result);
                }
                rts = value;
            }

            public override Boolean PurgeHwBuffers(Boolean purgeReadBuffers, Boolean purgeWriteBuffers)
            {
                if (purgeWriteBuffers)
                {
                    int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                            RESET_PURGE_RX, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                    if (result != 0)
                    {
                        throw new IOException("Flushing RX failed: result=" + result);
                    }
                }
                if (purgeReadBuffers)
                {
                    int result = mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                            RESET_PURGE_RX, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                    if (result != 0)
                    {
                        throw new IOException("Flushing RX failed: result=" + result);
                    }
                }

                return true;
            }
        }

        public static Dictionary<int, int[]> GetSupportedDevices()
        {
            return new Dictionary<int, int[]>
            {
                {
                    UsbId.VENDOR_FTDI, new int[]
                    {
                        UsbId.FTDI_FT232R,
                        UsbId.FTDI_FT232H,
                        UsbId.FTDI_FT2232H,
                        UsbId.FTDI_FT4232H,
                        UsbId.FTDI_FT231X,  // same ID for FT230X, FT231X, FT234XD
                    }
                }
            };
        }
    }
}
