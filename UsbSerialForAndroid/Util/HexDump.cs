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
        private static char[] HEX_DIGITS = {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
        };

        public static string DumpHexString(byte[] array)
        {
            return DumpHexString(array, 0, array.Length);
        }
        public static string DumpHexString(byte[] array, int offset, int length)
        {
            StringBuilder result = new StringBuilder();

            byte[] line = new byte[16];
            int lineIndex = 0;

            result.Append("\n0x");
            result.Append(ToHexString(offset));

            for (int i = offset; i < offset + length; i++)
            {
                if (lineIndex == 16)
                {
                    result.Append(" ");

                    for (int j = 0; j < 16; j++)
                    {
                        if (line[j] > ' ' && line[j] < '~')
                        {
                            result.Append(System.Text.Encoding.Default.GetString(line).Substring(j, 1));
                        }
                        else
                        {
                            result.Append(".");
                        }
                    }

                    result.Append("\n0x");
                    result.Append(ToHexString(i));
                    lineIndex = 0;
                }

                byte b = array[i];
                result.Append(" ");
                result.Append(HEX_DIGITS[(b >> 4) & 0x0F]);
                result.Append(HEX_DIGITS[b & 0x0F]);

                line[lineIndex++] = b;
            }

            if (lineIndex != 16)
            {
                int count = (16 - lineIndex) * 3;
                count++;
                for (int i = 0; i < count; i++)
                {
                    result.Append(" ");
                }

                for (int i = 0; i < lineIndex; i++)
                {
                    if (line[i] > ' ' && line[i] < '~')
                    {
                        result.Append(System.Text.Encoding.Default.GetString(line).Substring(i, 1));
                    }
                    else
                    {
                        result.Append(".");
                    }
                }
            }

            return result.ToString();
        }

        public static string ToHexString(byte[] byteArray)
        {
            return BitConverter.ToString(byteArray).Replace("-", "");
        }

        public static string ToHexString(byte[] byteArray, int offset, int length)
        {
            StringBuilder hex = new StringBuilder(length*2);

            while ((offset < byteArray.Length) && (length > 0))
            {
                hex.AppendFormat("{0:x2}", byteArray[offset]);

                offset++;
                length--;
            }
            return hex.ToString();
        }

        public static string ToHexString(int i)
        {
            return ToHexString(ToByteArray(i));
        }

        public static string ToHexString(short i)
        {
            return ToHexString(ToByteArray(i));
        }

        public static byte[] ToByteArray(byte b)
        {
            return new byte[] {b};
        }

        public static byte[] ToByteArray(int i)
        {
            byte[] array = new byte[4];

            array[3] = (byte) (i & 0xFF);
            array[2] = (byte) ((i >> 8) & 0xFF);
            array[1] = (byte) ((i >> 16) & 0xFF);
            array[0] = (byte) ((i >> 24) & 0xFF);

            return array;
        }

        public static byte[] ToByteArray(short i)
        {
            byte[] array = new byte[2];

            array[1] = (byte) (i & 0xFF);
            array[0] = (byte) ((i >> 8) & 0xFF);

            return array;
        }

    }
}