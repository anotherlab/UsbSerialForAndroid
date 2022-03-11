/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;

namespace Hoho.Android.UsbSerial.Extensions
{
    public class SerialDataReceivedArgs : EventArgs
    {
        public SerialDataReceivedArgs(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; private set; }
    }
}