/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.IO;
using System.Collections.Generic;

using Android.Hardware.Usb;
using Android.Util;

using Java.Nio;

namespace Hoho.Android.UsbSerial.Driver
{
	public class STM32SerialDriver : UsbSerialDriver
	{
		readonly string TAG = nameof(STM32SerialDriver);

		int mCtrlInterf;

		public STM32SerialDriver(UsbDevice device)
		{
			mDevice = device;
			mPort = new STM32SerialPort(mDevice, 0, this);
		}

		public class STM32SerialPort : CommonUsbSerialPort
		{
			readonly string TAG = nameof(STM32SerialDriver);

			readonly bool ENABLE_ASYNC_READS;
			UsbInterface mControlInterface;
			UsbInterface mDataInterface;

			UsbEndpoint mReadEndpoint;
			UsbEndpoint mWriteEndpoint;

			bool mRts = false;
			bool mDtr = false;

			IUsbSerialDriver Driver;

			const int USB_WRITE_TIMEOUT_MILLIS = 5000;

			const int USB_RECIP_INTERFACE = 0x01;
			const int USB_RT_AM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

			const int SET_LINE_CODING = 0x20; // USB CDC 1.1 section 6.2
			const int SET_CONTROL_LINE_STATE = 0x22;

			public STM32SerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
			{
				Driver = driver;
				ENABLE_ASYNC_READS = true;
			}

			public override IUsbSerialDriver GetDriver() =>
				Driver;

			int SendAcmControlMessage(int request, int value, byte[] buf) =>
				mConnection.ControlTransfer((UsbAddressing)USB_RT_AM, request, value, (Driver as STM32SerialDriver).mCtrlInterf, buf, buf?.Length ?? 0, USB_WRITE_TIMEOUT_MILLIS);

			public override void Open(UsbDeviceConnection connection)
			{
				if (mConnection != null)
					throw new IOException("Already opened.");

				mConnection = connection;
				bool opened = false;
				bool controlInterfaceFound = false;
				try
				{
					for (var i = 0; i < mDevice.InterfaceCount; i++)
					{
						mControlInterface = mDevice.GetInterface(i);
						if(mControlInterface.InterfaceClass == UsbClass.Comm)
						{
							if (!mConnection.ClaimInterface(mControlInterface, true))
								throw new IOException("Could not claim control interface");
							(Driver as STM32SerialDriver).mCtrlInterf = i;
							controlInterfaceFound = true;
							break;
						}
					}
					if (!controlInterfaceFound)
						throw new IOException("Could not claim control interface");
					for (var i = 0; i < mDevice.InterfaceCount; i++)
					{
						mDataInterface = mDevice.GetInterface(i);
						if(mDataInterface.InterfaceClass == UsbClass.CdcData)
						{
							if (!mConnection.ClaimInterface(mDataInterface, true))
								throw new IOException("Could not claim data interface");
							mReadEndpoint = mDataInterface.GetEndpoint(1);
							mWriteEndpoint = mDataInterface.GetEndpoint(0);
							opened = true;
							break;
						}
					}
					if(!opened)
						throw new IOException("Could not claim data interface.");
				}
				finally
				{
					if (!opened)
						mConnection = null;
				}
			}

			public override void Close()
			{
				if (mConnection == null)
					throw new IOException("Already closed");
				mConnection.Close();
				mConnection = null;
			}

			public override int Read(byte[] dest, int timeoutMillis)
			{
				if(ENABLE_ASYNC_READS)
				{
					var request = new UsbRequest();
					try
					{
						request.Initialize(mConnection, mReadEndpoint);
						ByteBuffer buf = ByteBuffer.Wrap(dest);
						if (!request.Queue(buf, dest.Length))
							throw new IOException("Error queuing request");

						UsbRequest response = mConnection.RequestWait();
						if (response == null)
							throw new IOException("Null response");

						int nread = buf.Position();
						if (nread > 0)
							return nread;

						return 0;
					}
					finally
					{
						request.Close();
					}
				}

				int numBytesRead;
				lock(mReadBufferLock)
				{
					int readAmt = Math.Min(dest.Length, mReadBuffer.Length);
					numBytesRead = mConnection.BulkTransfer(mReadEndpoint, mReadBuffer, readAmt, timeoutMillis);
					if(numBytesRead < 0)
					{
						// This sucks: we get -1 on timeout, not 0 as preferred.
						// We *should* use UsbRequest, except it has a bug/api oversight
						// where there is no way to determine the number of bytes read
						// in response :\ -- http://b.android.com/28023
						if (timeoutMillis == int.MaxValue)
						{
							// Hack: Special case "~infinite timeout" as an error.
							return -1;
						}

						return 0;
					}
					Array.Copy(mReadBuffer, 0, dest, 0, numBytesRead);
				}
				return numBytesRead;
			}

			public override int Write(byte[] src, int timeoutMillis)
			{
				int offset = 0;

				while(offset < src.Length)
				{
					int writeLength;
					int amtWritten;

					lock(mWriteBufferLock)
					{
						byte[] writeBuffer;

						writeLength = Math.Min(src.Length - offset, mWriteBuffer.Length);
						if (offset == 0)
							writeBuffer = src;
						else
						{
							Array.Copy(src, offset, mWriteBuffer, 0, writeLength);
							writeBuffer = mWriteBuffer;
						}

						amtWritten = mConnection.BulkTransfer(mWriteEndpoint, writeBuffer, writeLength, timeoutMillis);
					}
					if(amtWritten <= 0)
						throw new IOException($"Error writing {writeLength} bytes at offset {offset} length={src.Length}");

					Log.Debug(TAG, $"Wrote amt={amtWritten} attempted={writeLength}");
					offset += amtWritten;
				}

				return offset;
			}

			public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
			{
				byte stopBitsBytes;
				switch(stopBits)
				{
					case StopBits.One: 
						stopBitsBytes = 0;
						break;
					case StopBits.OnePointFive:
						stopBitsBytes = 1;
						break;
					case StopBits.Two:
						stopBitsBytes = 2;
						break;
					default:
						throw new ArgumentException($"Bad value for stopBits: {stopBits}");
				}

				byte parityBitesBytes;
				switch(parity)
				{
					case Parity.None:
						parityBitesBytes = 0;
						break;
					case Parity.Odd:
						parityBitesBytes = 1;
						break;
					case Parity.Even:
						parityBitesBytes = 2;
						break;
					case Parity.Mark:
						parityBitesBytes = 3;
						break;
					case Parity.Space:
						parityBitesBytes = 4;
						break;
					default:
						throw new ArgumentException($"Bad value for parity: {parity}");
				}

				byte[] msg = {
					(byte)(baudRate & 0xff),
					(byte) ((baudRate >> 8 ) & 0xff),
					(byte) ((baudRate >> 16) & 0xff),
					(byte) ((baudRate >> 24) & 0xff),
					stopBitsBytes,
					parityBitesBytes,
					(byte) dataBits
				};
				SendAcmControlMessage(SET_LINE_CODING, 0, msg);
			}

			public override bool GetCD() =>
				false; //TODO

			public override bool GetCTS() =>
				false; //TODO

			public override bool GetDSR() =>
				false; // TODO

			public override bool GetDTR() =>
				mDtr;

			public override void SetDTR(bool value)
			{
				mDtr = value;
				SetDtrRts();
			}

			public override bool GetRI() =>
				false; //TODO

			public override bool GetRTS() =>
				mRts; //TODO

			public override void SetRTS(bool value)
			{
				mRts = value;
				SetDtrRts();
			}

			void SetDtrRts()
			{
				int value = (mRts ? 0x2 : 0) | (mDtr ? 0x1 : 0);
				SendAcmControlMessage(SET_CONTROL_LINE_STATE, value, null);
			}

			public static Dictionary<int, int[]> GetSupportedDevices()
			{
				return new Dictionary<int, int[]>
				{
					{
						UsbId.VENDOR_STM, new int[]
						{
							UsbId.STM32_STLINK,
							UsbId.STM32_VCOM
						}
					}
				};
			}
		}
	}
}
