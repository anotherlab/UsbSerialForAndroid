/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using Android.Runtime;
using Java.Lang;

namespace Hoho.Android.UsbSerial.Driver
{
    public class UsbSerialRuntimeException : RuntimeException
    {
        public UsbSerialRuntimeException() : base()
        {
        }

        public UsbSerialRuntimeException(Throwable throwable) : base(throwable)
        {
        }

        public UsbSerialRuntimeException(string detailMessage) : base(detailMessage)
        {
        }

        public UsbSerialRuntimeException(string detailMessage, Throwable throwable) : base(detailMessage, throwable)
        {
        }

        protected UsbSerialRuntimeException(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }
    }
}