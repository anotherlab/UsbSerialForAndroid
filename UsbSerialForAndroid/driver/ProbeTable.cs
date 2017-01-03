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
using System.Reflection;
using Android.Util;

namespace Hoho.Android.UsbSerial.Driver
{
    public class ProbeTable
    {
        private readonly string TAG = typeof(ProbeTable).Name;

        Dictionary<Tuple<int, int>, Type> mProbeTable = new Dictionary<Tuple<int, int>, Type>();


        /**
         * Adds or updates a (vendor, product) pair in the table.
         *
         * @param vendorId the USB vendor id
         * @param productId the USB product id
         * @param driverClass the driver class responsible for this pair
         * @return {@code this}, for chaining
         */
        public ProbeTable AddProduct(int vendorId, int productId,
                Type driverClass)
        {
            var key = new Tuple<int, int>(vendorId, productId);

            if (!mProbeTable.ContainsKey(key))
                mProbeTable.Add(key, driverClass);

            return this;
        }

        public ProbeTable AddDriver(Type driverClass)
        {
            MethodInfo m = driverClass.GetMethod("GetSupportedDevices");

            var devices =  (Dictionary<int, int[]>)m.Invoke(null, null);

            foreach (var vendorId in devices.Keys)
            {
                var productIds = devices[vendorId];

                foreach (var productId in productIds)
                {
                    try
                    {
                        AddProduct(vendorId, productId, driverClass);
                        Log.Debug(TAG, $"Added {vendorId:X}, {productId:X}, {driverClass}");
                    }
                    catch (Exception)
                    {
                        Log.Debug(TAG, $"Error adding {vendorId:X}, {productId:X}, {driverClass}");
                        
                        throw;
                    }
                }
            }

            return this;

        }

        public Type FindDriver(int vendorId, int productId)
        {
            var pair = new Tuple<int, int>(vendorId, productId);

            return mProbeTable.ContainsKey(pair) ? mProbeTable[pair] : null;
        }

    }
}