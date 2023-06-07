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
using Java.Util;

namespace Hoho.Android.UsbSerial.Driver
{
    public class Ch34xSerialDriver : UsbSerialDriver
    {
        private readonly string TAG = typeof(ProlificSerialDriver).Name;

        public Ch34xSerialDriver(UsbDevice device)
        {
            mDevice = device;
            mPort = new Ch340SerialPort(mDevice, 0, this);
        }

        public class Ch340SerialPort : CommonUsbSerialPort
        {
            private static int USB_TIMEOUT_MILLIS = 5000;

            private int DEFAULT_BAUD_RATE = 9600;

            private const int SCL_DTR = 0x20;
            private const int SCL_RTS = 0x40;
            private const int LCR_ENABLE_RX = 0x80;
            private const int LCR_ENABLE_TX = 0x40;
            private const int LCR_STOP_BITS_2 = 0x04;
            private const int LCR_CS8 = 0x03;
            private const int LCR_CS7 = 0x02;
            private const int LCR_CS6 = 0x01;
            private const int LCR_CS5 = 0x00;

            private const int LCR_MARK_SPACE = 0x20;
            private const int LCR_PAR_EVEN = 0x10;
            private const int LCR_ENABLE_PAR = 0x08;

            private Boolean dtr = false;
            private Boolean rts = false;

            private UsbEndpoint mReadEndpoint;
            private UsbEndpoint mWriteEndpoint;

            private new readonly IUsbSerialDriver Driver;
            private string TAG => (Driver as Ch34xSerialDriver)?.TAG;

            public Ch340SerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
            {
                Driver = driver;
            }

            public override IUsbSerialDriver GetDriver()
            {
                return Driver;
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
                            Log.Debug(TAG, "claimInterface " + i + " SUCCESS");
                        }
                        else
                        {
                            Log.Debug(TAG, "claimInterface " + i + " FAIL");
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


                    Initialize();
                    SetBaudRate(DEFAULT_BAUD_RATE);

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

                // TODO: nothing sended on close, maybe needed?

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
                int numBytesRead;
                lock (mReadBufferLock)
                {
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

            private int ControlOut(int request, int value, int index)
            {
                int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirOut;
                return mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request,
                    value, index, null, 0, USB_TIMEOUT_MILLIS);
            }


            private int ControlIn(int request, int value, int index, byte[] buffer)
            {
                int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirIn;
                return mConnection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request,
                    value, index, buffer, buffer.Length, USB_TIMEOUT_MILLIS);
            }

            private void CheckState(String msg, int request, int value, int[] expected)
            {
                byte[] buffer = new byte[expected.Length];
                int ret = ControlIn(request, value, 0, buffer);

                if (ret < 0)
                {
                    throw new IOException($"Failed send cmd [{msg}]");
                }

                if (ret != expected.Length)
                {
                    throw new IOException($"Expected {expected.Length} bytes, but get {ret} [{msg}]");
                }

                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i] == -1)
                    {
                        continue;
                    }

                    int current = buffer[i] & 0xff;
                    if (expected[i] != current)
                    {
                        throw new IOException($"Expected 0x{expected[i]:X} bytes, but get 0x{current:X} [ {msg} ]");
                    }
                }
            }

            private void SetControlLines()
            {
                if (ControlOut(0xa4, ~((dtr ? SCL_DTR : 0) | (rts ? SCL_RTS : 0)), 0) < 0)
                {
                    throw new IOException("Failed to set control lines");
                }
            }

            private void Initialize()
            {
                CheckState("init #1", 0x5f, 0, new int[] { -1 /* 0x27, 0x30 */, 0x00 });

                if (ControlOut(0xa1, 0, 0) < 0)
                {
                    throw new IOException("init failed! #2");
                }

                SetBaudRate(DEFAULT_BAUD_RATE);

                CheckState("init #4", 0x95, 0x2518, new int[] { -1 /* 0x56, c3*/, 0x00 });

                if (ControlOut(0x9a, 0x2518, 0x0050) < 0)
                {
                    throw new IOException("init failed! #5");
                }

                CheckState("init #6", 0x95, 0x0706, new int[] { -1 /*0xf?*/, -1 /*0xec,0xee*/});

                if (ControlOut(0xa1, 0x501f, 0xd90a) < 0)
                {
                    throw new IOException("init failed! #7");
                }

                SetBaudRate(DEFAULT_BAUD_RATE);

                SetControlLines();

                CheckState("init #10", 0x95, 0x0706, new int[] { -1 /* 0x9f, 0xff*/, 0xee });
            }

            private void SetBaudRate(int baudRate)
            {
                int[] baud = new int[] { 50, 0x1680, 0x0024, 75, 0x6480, 0x0018, 100, 0x8B80, 0x0012,
				110, 0x9580, 0x00B4,  150, 0xB280, 0x000C, 300, 0xD980, 0x0006, 600, 0x6481, 0x0018,
				900, 0x9881, 0x0010, 1200, 0xB281, 0x000C, 1800, 0xCC81, 0x0008, 2400, 0xD981, 0x0006,
				3600, 0x3082, 0x0020, 4800, 0x6482, 0x0018, 9600, 0xB282, 0x000C, 14400, 0xCC82, 0x0008,
				19200, 0xD982, 0x0006, 33600, 0x4D83, 0x00D3, 38400, 0x6483, 0x0018, 56000, 0x9583, 0x0018,
				57600, 0x9883, 0x0010, 76800, 0xB283, 0x000C, 115200, 0xCC83, 0x0008, 128000, 0xD183, 0x003B,
				153600, 0xD983, 0x0006, 230400, 0xE683, 0x0004, 460800, 0xF383, 0x0002, 921600, 0xF387, 0x0000,
				1500000, 0xFC83, 0x0003, 2000000, 0xFD83, 0x0002 };

                for (int i = 0; i < baud.Length / 3; i++)
                {
                    if (baud[i * 3] == baudRate)
                    {
                        int ret = ControlOut(0x9a, 0x1312, baud[i * 3 + 1]);
                        if (ret < 0)
                        {
                            throw new IOException("Error setting baud rate. #1");
                        }
                        ret = ControlOut(0x9a, 0x0f2c, baud[i * 3 + 2]);
                        if (ret < 0)
                        {
                            throw new IOException("Error setting baud rate. #1");
                        }

                        return;
                    }
                }


                throw new IOException("Baud rate " + baudRate + " currently not supported");
            }

            public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
            {
                SetBaudRate(baudRate);

                int lcr = LCR_ENABLE_RX | LCR_ENABLE_TX;

                lcr |= dataBits switch
                {
                    DATABITS_5 => LCR_CS5,
                    DATABITS_6 => LCR_CS6,
                    DATABITS_7 => LCR_CS7,
                    DATABITS_8 => LCR_CS8,
                    _ => throw new Java.Lang.IllegalArgumentException("Invalid data bits: " + dataBits),
                };


                lcr |= (int)parity switch
                {
                    PARITY_NONE => lcr,
                    PARITY_ODD => LCR_ENABLE_PAR,
                    PARITY_EVEN => LCR_ENABLE_PAR | LCR_PAR_EVEN,
                    PARITY_MARK => LCR_ENABLE_PAR | LCR_MARK_SPACE,
                    PARITY_SPACE => LCR_ENABLE_PAR | LCR_MARK_SPACE | LCR_PAR_EVEN,
                    _ => throw new Java.Lang.IllegalArgumentException("Invalid parity: " + parity),
                };

                lcr |= (int)stopBits switch
                {
                    STOPBITS_1 => lcr,
                    STOPBITS_1_5 => throw new Java.Lang.UnsupportedOperationException("Unsupported stop bits: 1.5"),
                    STOPBITS_2 => LCR_STOP_BITS_2,
                    _ => throw new Java.Lang.IllegalArgumentException("Invalid stop bits: " + stopBits)
                };

                int ret = ControlOut(0x9a, 0x2518, lcr);
                if (ret < 0)
                {
                    throw new IOException("Error setting control byte");
                }
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
                return dtr;
            }

            public override void SetDTR(bool value)
            {
                dtr = value;
                SetControlLines();
            }

            public override bool GetRI()
            {
                return false;
            }

            public override bool GetRTS()
            {
                return rts;
            }

            public override void SetRTS(bool value)
            {
                rts = value;
                SetControlLines();
            }

            /*public EnumSet<ControlLine> getControlLines()
            {

                int status = getStatus();
                EnumSet<ControlLine> set = EnumSet.noneOf(ControlLine.class);
			    if(rts) set.add(ControlLine.RTS);
			    if((status & GCL_CTS) == 0) set.add(ControlLine.CTS);
			    if(dtr) set.add(ControlLine.DTR);
			    if((status & GCL_DSR) == 0) set.add(ControlLine.DSR);
			    if((status & GCL_CD) == 0) set.add(ControlLine.CD);
			    if((status & GCL_RI) == 0) set.add(ControlLine.RI);
			    return set;
		    }*/

        public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers)
        {
            return true;
        }
    }

    public static Dictionary<int, int[]> GetSupportedDevices()
    {
        return new Dictionary<int, int[]>
            {
                {
                    UsbId.VENDOR_QINHENG, new int[]
                    {
                        UsbId.QINHENG_HL340
                    }
                }
            };
    }
}
}
