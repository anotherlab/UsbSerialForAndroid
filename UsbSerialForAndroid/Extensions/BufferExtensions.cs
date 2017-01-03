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
using Java.Nio;

namespace Hoho.Android.UsbSerial.Extensions
{
    /// <summary>
    /// Work around for faulty JNI wrapping in Xamarin library.  Fixes a bug 
    /// where binding for Java.Nio.ByteBuffer.Get(byte[], int, int) allocates a new temporary 
    /// Java byte array on every call 
    /// See https://bugzilla.xamarin.com/show_bug.cgi?id=31260
    /// and http://stackoverflow.com/questions/30268400/xamarin-implementation-of-bytebuffer-get-wrong
    /// </summary>
    public static class BufferExtensions
    {
        static IntPtr _byteBufferClassRef;
        static IntPtr _byteBufferGetBii;

        public static ByteBuffer Get(this ByteBuffer buffer, JavaArray<Java.Lang.Byte> dst, int dstOffset, int byteCount)
        {
            if (_byteBufferClassRef == IntPtr.Zero)
            {
                _byteBufferClassRef = JNIEnv.FindClass("java/nio/ByteBuffer");
            }
            if (_byteBufferGetBii == IntPtr.Zero)
            {
                _byteBufferGetBii = JNIEnv.GetMethodID(_byteBufferClassRef, "get", "([BII)Ljava/nio/ByteBuffer;");
            }

            return Java.Lang.Object.GetObject<ByteBuffer>(JNIEnv.CallObjectMethod(buffer.Handle, _byteBufferGetBii, new JValue[] {
             new JValue(dst),
             new JValue(dstOffset),
             new JValue(byteCount)
         }), JniHandleOwnership.TransferLocalRef);
        }

        public static byte[] ToByteArray(this ByteBuffer buffer)
        {
            IntPtr classHandle = JNIEnv.FindClass("java/nio/ByteBuffer");
            IntPtr methodId = JNIEnv.GetMethodID(classHandle, "array", "()[B");
            IntPtr resultHandle = JNIEnv.CallObjectMethod(buffer.Handle, methodId);

            byte[] result = JNIEnv.GetArray<byte>(resultHandle);

            JNIEnv.DeleteLocalRef(resultHandle);

            return result;
        }
    }
}