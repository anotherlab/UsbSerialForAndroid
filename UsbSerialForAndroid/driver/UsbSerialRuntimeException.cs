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