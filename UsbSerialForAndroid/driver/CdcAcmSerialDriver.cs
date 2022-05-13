/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Hoho.Android.UsbSerial.Extensions;
using Hoho.Android.UsbSerial.Util;
using Java.Util;
using Java.IO;
using Java.Lang;
using Java.Nio;
using Math = System.Math;

namespace Hoho.Android.UsbSerial.Driver
{
    public class CdcAcmSerialDriver : UsbSerialDriver
    {
        private static string TAG = typeof(CdcAcmSerialDriver).Name;

        public CdcAcmSerialDriver(UsbDevice device, bool? enableAsyncReads = null)
        {
            mDevice = device;
            mPort = new CdcAcmSerialPort(device, 0, this, enableAsyncReads);
        }

        // Additional constructor for ProbeDevice called by Reflection
        // Note that this defaults to enableAsyncReads = false as
        // there is no support for passing additional arguments from ProbeDevice
        public CdcAcmSerialDriver(UsbDevice device)
        {
            mDevice = device;
            bool enableAsyncReads = false;  // For clarity of understanding
            mPort = new CdcAcmSerialPort(device, 0, this, enableAsyncReads);
        }

        class CdcAcmSerialPort : CommonUsbSerialPort
        {
            private bool mEnableAsyncReads;
            private UsbInterface mControlInterface;
            private UsbInterface mDataInterface;

            private UsbEndpoint mControlEndpoint;
            private UsbEndpoint mReadEndpoint;
            private UsbEndpoint mWriteEndpoint;

            private bool mRts = false;
            private bool mDtr = false;

            private static int USB_RECIP_INTERFACE = 0x01;
            private static int USB_RT_ACM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

            private static int SET_LINE_CODING = 0x20;  // USB CDC 1.1 section 6.2
            private static int GET_LINE_CODING = 0x21;
            private static int SET_CONTROL_LINE_STATE = 0x22;
            private static int SEND_BREAK = 0x23;

            private IUsbSerialDriver Driver;

            //public CdcAcmSerialPort(UsbDevice device, int portNumber) : base(device, portNumber)
            //{
            //    mEnableAsyncReads = (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1);
            //}

            public CdcAcmSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver, bool? enableAsyncReads = null) : base(device, portNumber)
            {
                if (enableAsyncReads != null)
                {
                    mEnableAsyncReads = enableAsyncReads.Value;
                }
                else
                {
                    mEnableAsyncReads = (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1);

                }
                this.Driver = driver;
            }


            public override IUsbSerialDriver GetDriver()
            {
                return Driver;
            }

            public override void Open(UsbDeviceConnection connection)
            {
                if (mConnection != null)
                {
                    throw new IOException("Already open");
                }

                mConnection = connection;
                bool opened = false;

                try
                {
                    if (1 == mDevice.InterfaceCount)
                    {
                        Log.Debug(TAG, "device might be castrated ACM device, trying single interface logic");
                        OpenSingleInterface();
                    }
                    else
                    {
                        Log.Debug(TAG, "trying default interface logic");
                        openInterface();
                    }

                    if (mEnableAsyncReads)
                    {
                        Log.Debug(TAG, "Async reads enabled");
                    }
                    else
                    {
                        Log.Debug(TAG, "Async reads disabled.");
                    }


                    opened = true;
                }
                finally
                {
                    if (!opened)
                    {
                        mConnection = null;
                        // just to be on the save side
                        mControlEndpoint = null;
                        mReadEndpoint = null;
                        mWriteEndpoint = null;
                    }
                }
            }

            private void OpenSingleInterface()
            {
                // the following code is inspired by the cdc-acm driver
                // in the linux kernel

                mControlInterface = mDevice.GetInterface(0);
                Log.Debug(TAG, "Control iface=" + mControlInterface);

                mDataInterface = mDevice.GetInterface(0);
                Log.Debug(TAG, "data iface=" + mDataInterface);

                if (!mConnection.ClaimInterface(mControlInterface, true))
                {
                    throw new IOException("Could not claim shared control/data interface.");
                }

                int endCount = mControlInterface.EndpointCount;

                if (endCount < 3)
                {
                    Log.Debug(TAG, "not enough endpoints - need 3. count=" + endCount);
                    throw new IOException("Insufficient number of endpoints(" + endCount + ")");
                }

                // Analyse endpoints for their properties
                mControlEndpoint = null;
                mReadEndpoint = null;
                mWriteEndpoint = null;
                for (int i = 0; i < endCount; ++i)
                {
                    UsbEndpoint ep = mControlInterface.GetEndpoint(i);
                    if ((ep.Direction == UsbAddressing.In) &&
                        (ep.Type == UsbAddressing.XferInterrupt))
                    {
                        Log.Debug(TAG, "Found controlling endpoint");
                        mControlEndpoint = ep;
                    }
                    else if ((ep.Direction == UsbAddressing.In) &&
                             (ep.Type == UsbAddressing.XferBulk))
                    {
                        Log.Debug(TAG, "Found reading endpoint");
                        mReadEndpoint = ep;
                    }
                    else if ((ep.Direction == UsbAddressing.Out) &&
                          (ep.Type == UsbAddressing.XferBulk))
                    {
                        Log.Debug(TAG, "Found writing endpoint");
                        mWriteEndpoint = ep;
                    }


                    if ((mControlEndpoint != null) &&
                        (mReadEndpoint != null) &&
                        (mWriteEndpoint != null))
                    {
                        Log.Debug(TAG, "Found all required endpoints");
                        break;
                    }
                }

                if ((mControlEndpoint == null) ||
                        (mReadEndpoint == null) ||
                        (mWriteEndpoint == null))
                {
                    Log.Debug(TAG, "Could not establish all endpoints");
                    throw new IOException("Could not establish all endpoints");
                }
            }

            private void openInterface()
            {
                Log.Debug(TAG, "claiming interfaces, count=" + mDevice.InterfaceCount);

                mControlInterface = mDevice.GetInterface(0);
                Log.Debug(TAG, "Control iface=" + mControlInterface);
                // class should be USB_CLASS_COMM

                if (!mConnection.ClaimInterface(mControlInterface, true))
                {
                    throw new IOException("Could not claim control interface.");
                }

                mControlEndpoint = mControlInterface.GetEndpoint(0);
                Log.Debug(TAG, "Control endpoint direction: " + mControlEndpoint.Direction);

                Log.Debug(TAG, "Claiming data interface.");
                mDataInterface = mDevice.GetInterface(1);
                Log.Debug(TAG, "data iface=" + mDataInterface);
                // class should be USB_CLASS_CDC_DATA

                if (!mConnection.ClaimInterface(mDataInterface, true))
                {
                    throw new IOException("Could not claim data interface.");
                }
                mReadEndpoint = mDataInterface.GetEndpoint(1);
                Log.Debug(TAG, "Read endpoint direction: " + mReadEndpoint.Direction);
                mWriteEndpoint = mDataInterface.GetEndpoint(0);
                Log.Debug(TAG, "Write endpoint direction: " + mWriteEndpoint.Direction);
            }

            private int SendAcmControlMessage(int request, int value, byte[] buf)
            {
                return mConnection.ControlTransfer((UsbAddressing)0x21,
                        request, value, 0, buf, buf != null ? buf.Length : 0, 5000);
            }

            public override void Close()
            {
                if (mConnection == null)
                {
                    throw new IOException("Already closed");
                }
                mConnection.Close();
                mConnection = null;
            }

            public override int Read(byte[] dest, int timeoutMillis)
            {
                if (mEnableAsyncReads)
                {
                    UsbRequest request = new UsbRequest();
                    try
                    {
                        request.Initialize(mConnection, mReadEndpoint);

                        // CJM: Xamarin bug:  ByteBuffer.Wrap is supposed to be a two way update
                        // Changes made to one buffer should reflect in the other.  It's not working
                        // As a work around, I added a new method as an extension that uses JNI to turn
                        // a new byte[] array.  I then used BlockCopy to copy the bytes back the original array
                        // see https://forums.xamarin.com/discussion/comment/238396/#Comment_238396
                        //
                        // Old work around:
                        // as a work around, we populate dest with a call to buf.Get()
                        // see https://bugzilla.xamarin.com/show_bug.cgi?id=20772
                        // and https://bugzilla.xamarin.com/show_bug.cgi?id=31260

                        ByteBuffer buf = ByteBuffer.Wrap(dest);
                        if (!request.Queue(buf, dest.Length))
                        {
                            throw new IOException("Error queueing request.");
                        }

                        UsbRequest response = mConnection.RequestWait();
                        if (response == null)
                        {
                            throw new IOException("Null response");
                        }

                        int nread = buf.Position();
                        if (nread > 0)
                        {
                            // CJM: This differs from the Java implementation.  The dest buffer was
                            // not getting the data back.

                            // 1st work around, no longer used
                            //buf.Rewind();
                            //buf.Get(dest, 0, dest.Length);

                            System.Buffer.BlockCopy(buf.ToByteArray(), 0, dest, 0, dest.Length);

                            Log.Debug(TAG, HexDump.DumpHexString(dest, 0, Math.Min(32, dest.Length)));
                            return nread;
                        }
                        else
                        {
                            return 0;
                        }
                    }
                    finally
                    {
                        request.Close();
                    }
                }

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
                        if (timeoutMillis == Integer.MaxValue)
                        {
                            // Hack: Special case "~infinite timeout" as an error.
                            return -1;
                        }
                        return 0;
                    }
                    System.Buffer.BlockCopy(mReadBuffer, 0, dest, 0, numBytesRead);
                }
                return numBytesRead;
            }

            public override int Write(byte[] src, int timeoutMillis)
            {
                // TODO(mikey): Nearly identical to FtdiSerial write. Refactor.
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
                            System.Buffer.BlockCopy(src, offset, mWriteBuffer, 0, writeLength);
                            writeBuffer = mWriteBuffer;
                        }

                        amtWritten = mConnection.BulkTransfer(mWriteEndpoint, writeBuffer, writeLength,
                                timeoutMillis);
                    }
                    if (amtWritten <= 0)
                    {
                        throw new IOException("Error writing " + writeLength
                                + " bytes at offset " + offset + " length=" + src.Length);
                    }

                    Log.Debug(TAG, "Wrote amt=" + amtWritten + " attempted=" + writeLength);
                    offset += amtWritten;
                }
                return offset;
            }


            public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
            {
                byte stopBitsByte;
                switch (stopBits)
                {
                    case StopBits.One: stopBitsByte = 0; break;
                    case StopBits.OnePointFive: stopBitsByte = 1; break;
                    case StopBits.Two: stopBitsByte = 2; break;
                    default: throw new IllegalArgumentException("Bad value for stopBits: " + stopBits);
                }

                byte parityBitesByte;
                switch (parity)
                {
                    case Parity.None: parityBitesByte = 0; break;
                    case Parity.Odd: parityBitesByte = 1; break;
                    case Parity.Even: parityBitesByte = 2; break;
                    case Parity.Mark: parityBitesByte = 3; break;
                    case Parity.Space: parityBitesByte = 4; break;
                    default: throw new IllegalArgumentException("Bad value for parity: " + parity);
                }

                byte[] msg = {
                                (byte) ( baudRate & 0xff),
                                (byte) ((baudRate >> 8 ) & 0xff),
                                (byte) ((baudRate >> 16) & 0xff),
                                (byte) ((baudRate >> 24) & 0xff),
                                stopBitsByte,
                                parityBitesByte,
                                (byte) dataBits};
                SendAcmControlMessage(SET_LINE_CODING, 0, msg);
            }

            public override bool GetCD()
            {
                return false;  // TODO
            }

            public override bool GetCTS()
            {
                return false;  // TODO
            }

            public override bool GetDSR()
            {
                return false;  // TODO
            }

            public override bool GetDTR()
            {
                return mDtr;
            }

            public override void SetDTR(bool value)
            {
                mDtr = value;
                SetDtrRts();
            }

            public override bool GetRI()
            {
                return false;  // TODO
            }

            public override bool GetRTS()
            {
                return mRts;
            }

            public override void SetRTS(bool value)
            {
                mRts = value;
                SetDtrRts();
            }

            private void SetDtrRts()
            {
                int value = (mRts ? 0x2 : 0) | (mDtr ? 0x1 : 0);
                SendAcmControlMessage(SET_CONTROL_LINE_STATE, value, null);
            }
        }

        public static Dictionary<int, int[]> GetSupportedDevices()
        {
            return new Dictionary<int, int[]>
            {
                {
                    UsbId.VENDOR_ARDUINO, new[]
                    {
                        UsbId.ARDUINO_UNO,
                        UsbId.ARDUINO_UNO_R3,
                        UsbId.ARDUINO_MEGA_2560,
                        UsbId.ARDUINO_MEGA_2560_R3,
                        UsbId.ARDUINO_SERIAL_ADAPTER,
                        UsbId.ARDUINO_SERIAL_ADAPTER_R3,
                        UsbId.ARDUINO_MEGA_ADK,
                        UsbId.ARDUINO_MEGA_ADK_R3,
                        UsbId.ARDUINO_LEONARDO,
                        UsbId.ARDUINO_MICRO,
                    }
                },
                {
                    UsbId.VENDOR_VAN_OOIJEN_TECH, new[]
                    {
                        UsbId.VAN_OOIJEN_TECH_TEENSYDUINO_SERIAL
                    }
                },
                {
                    UsbId.VENDOR_ATMEL, new[]
                    {
                        UsbId.ATMEL_LUFA_CDC_DEMO_APP
                    }
                },
                {
                    UsbId.VENDOR_ELATEC, new[]
                    {
                        UsbId.ELATEC_TWN3_CDC,
                        UsbId.ELATEC_TWN4_MIFARE_NFC,
                        UsbId.ELATEC_TWN4_CDC,
                    }
                },
                {
                    UsbId.VENDOR_LEAFLABS, new[]
                    {
                        UsbId.LEAFLABS_MAPLE
                    }
                }
            };
        }
    }


}