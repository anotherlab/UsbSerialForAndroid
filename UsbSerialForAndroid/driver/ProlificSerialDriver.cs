/* Copyright 2017 Tyler Technologies Inc.
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
 * USA.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Android.Hardware.Usb;
using Android.Util;
using Java.Lang;
using Javax.Security.Auth;
using Boolean = System.Boolean;
using Exception = Java.Lang.Exception;
using Object = System.Object;
using String = System.String;
using Thread = System.Threading.Thread;

namespace Hoho.Android.UsbSerial.Driver
{
    public class ProlificSerialDriver : UsbSerialDriver
    {
        private readonly string TAG = typeof (ProlificSerialDriver).Name;

        public ProlificSerialDriver(UsbDevice device)
        {
            mDevice = device;
            mPort = new ProlificSerialPort(mDevice, 0, this);
        }

        public static Dictionary<int, int[]> GetSupportedDevices()
        {
            return new Dictionary<int, int[]>
            {
                {
                    UsbId.VENDOR_PROLIFIC, new int[]
                    {
                        UsbId.VENDOR_PROLIFIC
                    }
                }
            };
        }


        class ProlificSerialPort : CommonUsbSerialPort
        {
            private static int USB_READ_TIMEOUT_MILLIS = 1000;
            private static int USB_WRITE_TIMEOUT_MILLIS = 5000;

            private static int USB_RECIP_INTERFACE = 0x01;

            private static int PROLIFIC_VENDOR_READ_REQUEST = 0x01;
            private static int PROLIFIC_VENDOR_WRITE_REQUEST = 0x01;

            private static int PROLIFIC_VENDOR_OUT_REQTYPE = UsbSupport.UsbDirOut
                                                             | UsbConstants.UsbTypeVendor;

            private static int PROLIFIC_VENDOR_IN_REQTYPE = UsbSupport.UsbDirIn
                                                            | UsbConstants.UsbTypeVendor;

            private static int PROLIFIC_CTRL_OUT_REQTYPE = UsbSupport.UsbDirOut
                                                           | UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

            private const int WRITE_ENDPOINT = 0x02;
            private const int READ_ENDPOINT = 0x83;
            private const int INTERRUPT_ENDPOINT = 0x81;

            private static int FLUSH_RX_REQUEST = 0x08;
            private static int FLUSH_TX_REQUEST = 0x09;

            private static int SET_LINE_REQUEST = 0x20;
            private static int SET_CONTROL_REQUEST = 0x22;

            private static int CONTROL_DTR = 0x01;
            private static int CONTROL_RTS = 0x02;

            private static int STATUS_FLAG_CD = 0x01;
            private static int STATUS_FLAG_DSR = 0x02;
            private static int STATUS_FLAG_RI = 0x08;
            private static int STATUS_FLAG_CTS = 0x80;

            private static int STATUS_BUFFER_SIZE = 10;
            private static int STATUS_BYTE_IDX = 8;

            private static int DEVICE_TYPE_HX = 0;
            private static int DEVICE_TYPE_0 = 1;
            private static int DEVICE_TYPE_1 = 2;

            private int mDeviceType = DEVICE_TYPE_HX;

            private UsbEndpoint mReadEndpoint;
            private UsbEndpoint mWriteEndpoint;
            private UsbEndpoint mInterruptEndpoint;

            private int mControlLinesValue = 0;

            private int mBaudRate = -1, mDataBits = -1;
            private StopBits mStopBits = StopBits.NotSet;
            private Parity mParity = Parity.NotSet;

            private int mStatus = 0;
            private volatile Thread mReadStatusThread = null;
            private Object mReadStatusThreadLock = new Object();
            Boolean mStopReadStatusThread = false;
            private IOException mReadStatusException = null;

            private IUsbSerialDriver Driver;

            private string TAG => (Driver as ProlificSerialDriver)?.TAG;

            public ProlificSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver)
                : base(device, portNumber)
            {
                Driver = driver;
            }

            public override IUsbSerialDriver GetDriver()
            {
                return Driver;
            }

            private byte[] InControlTransfer(int requestType, int request,
                int value, int index, int length)
            {
                byte[] buffer = new byte[length];
                int result = mConnection.ControlTransfer((UsbAddressing) requestType, request, value,
                    index, buffer, length, USB_READ_TIMEOUT_MILLIS);
                if (result != length)
                {
                    throw new IOException($"ControlTransfer with value {value} failed: {result}");
                }
                return buffer;
            }

            private void OutControlTransfer(int requestType, int request,
                int value, int index, byte[] data)
            {
                int length = data?.Length ?? 0;
                int result = mConnection.ControlTransfer((UsbAddressing) requestType, request, value,
                    index, data, length, USB_WRITE_TIMEOUT_MILLIS);
                if (result != length)
                {
                    throw new IOException($"ControlTransfer with value {value} failed: {result}");
                }
            }

            private byte[] VendorIn(int value, int index, int length)
            {
                return InControlTransfer(PROLIFIC_VENDOR_IN_REQTYPE,
                    PROLIFIC_VENDOR_READ_REQUEST, value, index, length);
            }

            private void VendorOut(int value, int index, byte[] data)
            {
                OutControlTransfer(PROLIFIC_VENDOR_OUT_REQTYPE,
                    PROLIFIC_VENDOR_WRITE_REQUEST, value, index, data);
            }

            private void ResetDevice()
            {
                PurgeHwBuffers(true, true);
            }

            private void CtrlOut(int request, int value, int index, byte[] data)
            {
                OutControlTransfer(PROLIFIC_CTRL_OUT_REQTYPE, request, value, index,
                    data);
            }

            private void DoBlackMagic()
            {
                VendorIn(0x8484, 0, 1);
                VendorOut(0x0404, 0, null);
                VendorIn(0x8484, 0, 1);
                VendorIn(0x8383, 0, 1);
                VendorIn(0x8484, 0, 1);
                VendorOut(0x0404, 1, null);
                VendorIn(0x8484, 0, 1);
                VendorIn(0x8383, 0, 1);
                VendorOut(0, 1, null);
                VendorOut(1, 0, null);
                VendorOut(2, (mDeviceType == DEVICE_TYPE_HX) ? 0x44 : 0x24, null);
            }

            private void SetControlLines(int newControlLinesValue)
            {
                CtrlOut(SET_CONTROL_REQUEST, newControlLinesValue, 0, null);
                mControlLinesValue = newControlLinesValue;
            }

            private void ReadStatusThreadFunction()
            {
                try
                {
                    while (!mStopReadStatusThread)
                    {
                        byte[] buffer = new byte[STATUS_BUFFER_SIZE];
                        int readBytesCount = mConnection.BulkTransfer(mInterruptEndpoint,
                            buffer,
                            STATUS_BUFFER_SIZE,
                            500);
                        if (readBytesCount > 0)
                        {
                            if (readBytesCount == STATUS_BUFFER_SIZE)
                            {
                                mStatus = buffer[STATUS_BYTE_IDX] & 0xff;
                            }
                            else
                            {
                                throw new IOException(
                                    $"Invalid CTS / DSR / CD / RI status buffer received, expected {STATUS_BUFFER_SIZE} bytes, but received {readBytesCount}");
                            }
                        }
                    }
                }
                catch (IOException e)
                {
                    mReadStatusException = e;
                }
            }

            private int GetStatus()
            {
                if ((mReadStatusThread == null) && (mReadStatusException == null))
                {
                    lock (mReadStatusThreadLock)
                    {
                        if (mReadStatusThread == null)
                        {
                            byte[] buffer = new byte[STATUS_BUFFER_SIZE];
                            int readBytes = mConnection.BulkTransfer(mInterruptEndpoint,
                                buffer,
                                STATUS_BUFFER_SIZE,
                                100);
                            if (readBytes != STATUS_BUFFER_SIZE)
                            {
                                Log.Warn(TAG, "Could not read initial CTS / DSR / CD / RI status");
                            }
                            else
                            {
                                mStatus = buffer[STATUS_BYTE_IDX] & 0xff;
                            }

                            ThreadStart mReadStatusThreadDelegate = new ThreadStart(ReadStatusThreadFunction);

                            mReadStatusThread = new Thread(mReadStatusThreadDelegate);

                            mReadStatusThread.Start();

                            //mReadStatusThread = new Thread(new Runnable()
                            //{
                            //    public void run()
                            //    {
                            //        ReadStatusThreadFunction();
                            //    }
                            //});
                            //mReadStatusThread.Daemon = true;//  setDaemon(true);
                            //mReadStatusThread.Start();
                        }
                    }
                }


                /* throw and clear an exception which occured in the status read thread */
                IOException readStatusException = mReadStatusException;
                if (mReadStatusException != null)
                {
                    mReadStatusException = null;
                    throw readStatusException;
                }

                return mStatus;
            }

            private Boolean TestStatusFlag(int flag)
            {
                return ((GetStatus() & flag) == flag);
            }

            public override void Open(UsbDeviceConnection connection)
            {
                if (mConnection != null)
                {
                    throw new IOException("Already open");
                }

                UsbInterface usbInterface = mDevice.GetInterface(0);

                if (!connection.ClaimInterface(usbInterface, true))
                {
                    throw new IOException("Error claiming Prolific interface 0");
                }
                mConnection = connection;
                Boolean opened = false;
                try
                {
                    for (int i = 0; i < usbInterface.EndpointCount; ++i)
                    {
                        UsbEndpoint currentEndpoint = usbInterface.GetEndpoint(i);

                        switch (currentEndpoint.Address)
                        {
                            case (UsbAddressing) READ_ENDPOINT:
                                mReadEndpoint = currentEndpoint;
                                break;

                            case (UsbAddressing) WRITE_ENDPOINT:
                                mWriteEndpoint = currentEndpoint;
                                break;

                            case (UsbAddressing) INTERRUPT_ENDPOINT:
                                mInterruptEndpoint = currentEndpoint;
                                break;
                        }
                    }

                    if (mDevice.DeviceClass == (UsbClass) 0x02)
                    {
                        mDeviceType = DEVICE_TYPE_0;
                    }
                    else
                    {
                        try
                        {
                            //Method getRawDescriptorsMethod
                            //    = mConnection.getClass().getMethod("getRawDescriptors");
                            //byte[] rawDescriptors
                            //    = (byte[])getRawDescriptorsMethod.invoke(mConnection);

                            byte[] rawDescriptors = mConnection.GetRawDescriptors();

                            byte maxPacketSize0 = rawDescriptors[7];
                            if (maxPacketSize0 == 64)
                            {
                                mDeviceType = DEVICE_TYPE_HX;
                            }
                            else if ((mDevice.DeviceClass == 0x00)
                                     || (mDevice.DeviceClass == (UsbClass) 0xff))
                            {
                                mDeviceType = DEVICE_TYPE_1;
                            }
                            else
                            {
                                Log.Warn(TAG, "Could not detect PL2303 subtype, "
                                              + "Assuming that it is a HX device");
                                mDeviceType = DEVICE_TYPE_HX;
                            }
                        }
                        catch (NoSuchMethodException e)
                        {
                            Log.Warn(TAG, "Method UsbDeviceConnection.getRawDescriptors, "
                                          + "required for PL2303 subtype detection, not "
                                          + "available! Assuming that it is a HX device");
                            mDeviceType = DEVICE_TYPE_HX;
                        }
                        catch (Exception e)
                        {
                            Log.Error(TAG, "An unexpected exception occured while trying "
                                           + "to detect PL2303 subtype", e);
                        }
                    }

                    SetControlLines(mControlLinesValue);
                    ResetDevice();

                    DoBlackMagic();
                    opened = true;
                }
                finally
                {
                    if (!opened)
                    {
                        mConnection = null;
                        connection.ReleaseInterface(usbInterface);
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
                    mStopReadStatusThread = true;
                    lock (mReadStatusThreadLock)
                    {
                        if (mReadStatusThread != null)
                        {
                            try
                            {
                                mReadStatusThread.Join();
                            }
                            catch (Exception e)
                            {
                                Log.Warn(TAG, "An error occured while waiting for status read thread", e);
                            }
                        }
                    }
                    ResetDevice();
                }
                finally
                {
                    try
                    {
                        mConnection.ReleaseInterface(mDevice.GetInterface(0));
                    }
                    finally
                    {
                        mConnection = null;
                    }
                }
            }

            public override int Read(byte[] dest, int timeoutMillis)
            {
                lock (mReadBufferLock)
                {
                    int readAmt = System.Math.Min(dest.Length, mReadBuffer.Length);
                    int numBytesRead = mConnection.BulkTransfer(mReadEndpoint, mReadBuffer,
                        readAmt, timeoutMillis);
                    if (numBytesRead < 0)
                    {
                        return 0;
                    }
                    Buffer.BlockCopy(mReadBuffer, 0, dest, 0, numBytesRead);
                    return numBytesRead;
                }
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

                        writeLength = System.Math.Min(src.Length - offset, mWriteBuffer.Length);
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

                        amtWritten = mConnection.BulkTransfer(mWriteEndpoint,
                            writeBuffer, writeLength, timeoutMillis);
                    }

                    if (amtWritten <= 0)
                    {
                        throw new IOException(
                            $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                    }

                    offset += amtWritten;
                }
                return offset;
            }

            public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
            {
                if ((mBaudRate == baudRate) && (mDataBits == dataBits)
                    && (mStopBits == stopBits) && (mParity == parity))
                {
                    // Make sure no action is performed if there is nothing to change
                    return;
                }

                byte[] lineRequestData = new byte[7];

                lineRequestData[0] = (byte) (baudRate & 0xff);
                lineRequestData[1] = (byte) ((baudRate >> 8) & 0xff);
                lineRequestData[2] = (byte) ((baudRate >> 16) & 0xff);
                lineRequestData[3] = (byte) ((baudRate >> 24) & 0xff);

                switch (stopBits)
                {
                    case StopBits.One:
                        lineRequestData[4] = 0;
                        break;

                    case StopBits.OnePointFive:
                        lineRequestData[4] = 1;
                        break;

                    case StopBits.Two:
                        lineRequestData[4] = 2;
                        break;

                    default:
                        throw new IllegalArgumentException("Unknown stopBits value: " + stopBits);
                }

                switch (parity)
                {
                    case Parity.None:
                        lineRequestData[5] = 0;
                        break;

                    case Parity.Odd:
                        lineRequestData[5] = 1;
                        break;

                    case Parity.Even:
                        lineRequestData[5] = 2;
                        break;

                    case Parity.Mark:
                        lineRequestData[5] = 3;
                        break;

                    case Parity.Space:
                        lineRequestData[5] = 4;
                        break;

                    default:
                        throw new IllegalArgumentException("Unknown parity value: " + parity);
                }

                lineRequestData[6] = (byte) dataBits;

                CtrlOut(SET_LINE_REQUEST, 0, 0, lineRequestData);

                ResetDevice();

                mBaudRate = baudRate;
                mDataBits = dataBits;
                mStopBits = stopBits;
                mParity = parity;
            }

            public override Boolean GetCD()
            {
                return TestStatusFlag(STATUS_FLAG_CD);
            }


            public override Boolean GetCTS()
            {
                return TestStatusFlag(STATUS_FLAG_CTS);
            }


            public override Boolean GetDSR()
            {
                return TestStatusFlag(STATUS_FLAG_DSR);
            }


            public override Boolean GetDTR()
            {
                return ((mControlLinesValue & CONTROL_DTR) == CONTROL_DTR);
            }


            public override void SetDTR(Boolean value)
            {
                int newControlLinesValue;
                if (value)
                {
                    newControlLinesValue = mControlLinesValue | CONTROL_DTR;
                }
                else
                {
                    newControlLinesValue = mControlLinesValue & ~CONTROL_DTR;
                }
                SetControlLines(newControlLinesValue);
            }


            public override Boolean GetRI()
            {
                return TestStatusFlag(STATUS_FLAG_RI);
            }


            public override Boolean GetRTS()
            {
                return ((mControlLinesValue & CONTROL_RTS) == CONTROL_RTS);
            }


            public override void SetRTS(Boolean value)
            {
                int newControlLinesValue;
                if (value)
                {
                    newControlLinesValue = mControlLinesValue | CONTROL_RTS;
                }
                else
                {
                    newControlLinesValue = mControlLinesValue & ~CONTROL_RTS;
                }
                SetControlLines(newControlLinesValue);
            }

            public override Boolean PurgeHwBuffers(Boolean purgeReadBuffers, Boolean purgeWriteBuffers)
            {
                if (purgeReadBuffers)
                {
                    VendorOut(FLUSH_RX_REQUEST, 0, null);
                }

                if (purgeWriteBuffers)
                {
                    VendorOut(FLUSH_TX_REQUEST, 0, null);
                }

                return purgeReadBuffers || purgeWriteBuffers;
            }
        }
    }
}