/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Threading;

namespace Hoho.Android.UsbSerial.Extensions
{
    static class EventHandlerExtensions
    {
        public static void Raise(this EventHandler handler, object sender, EventArgs e)
        {
            Volatile.Read(ref handler)?.Invoke(sender, e);
        }

        public static void Raise<T>(this EventHandler<T> handler, object sender, T e) where T : EventArgs
        {
            Volatile.Read(ref handler)?.Invoke(sender, e);
        }
    }
}