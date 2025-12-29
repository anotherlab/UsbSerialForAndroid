/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Text;

namespace Hoho.Android.UsbSerial.Util
{
    public class HexDump
    {
        public static string ByteArrayToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        public static string ByteArrayToHexString(byte[] bytes, int maxLength)
        {
            if (bytes == null || bytes.Length == 0 || maxLength <= 0)
                return string.Empty;

            int length = Math.Min(bytes.Length, maxLength);
            var sb = new StringBuilder(length * 3);

            for (int i = 0; i < length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));

                if (i < length - 1)
                    sb.Append(' ');
            }

            return sb.ToString();
        }
    }
}