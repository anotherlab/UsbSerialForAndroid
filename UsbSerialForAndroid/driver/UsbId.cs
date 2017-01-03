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

namespace Hoho.Android.UsbSerial.Driver
{
    /**
     * Registry of USB vendor/product ID constants.
     *
     * Culled from various sources; see
     * <a href="http://www.linux-usb.org/usb.ids">usb.ids</a> for one listing.
     *
     * @author mike wakerly (opensource@hoho.com)
     */
    public static class UsbId
    {
        public static readonly int VENDOR_FTDI = 0x0403;
        public static readonly int FTDI_FT232R = 0x6001;
        public static readonly int FTDI_FT231X = 0x6015;

        public static readonly int VENDOR_ATMEL = 0x03EB;
        public static readonly int ATMEL_LUFA_CDC_DEMO_APP = 0x2044;

        public static readonly int VENDOR_ARDUINO = 0x2341;
        public static readonly int ARDUINO_UNO = 0x0001;
        public static readonly int ARDUINO_MEGA_2560 = 0x0010;
        public static readonly int ARDUINO_SERIAL_ADAPTER = 0x003b;
        public static readonly int ARDUINO_MEGA_ADK = 0x003f;
        public static readonly int ARDUINO_MEGA_2560_R3 = 0x0042;
        public static readonly int ARDUINO_UNO_R3 = 0x0043;
        public static readonly int ARDUINO_MEGA_ADK_R3 = 0x0044;
        public static readonly int ARDUINO_SERIAL_ADAPTER_R3 = 0x0044;
        public static readonly int ARDUINO_LEONARDO = 0x8036;
        public static readonly int ARDUINO_MICRO = 0x8037;

        public static readonly int VENDOR_VAN_OOIJEN_TECH = 0x16c0;
        public static readonly int VAN_OOIJEN_TECH_TEENSYDUINO_SERIAL = 0x0483;

        public static readonly int VENDOR_LEAFLABS = 0x1eaf;
        public static readonly int LEAFLABS_MAPLE = 0x0004;

        public static readonly int VENDOR_SILABS = 0x10c4;
        public static readonly int SILABS_CP2102 = 0xea60;
        public static readonly int SILABS_CP2105 = 0xea70;
        public static readonly int SILABS_CP2108 = 0xea71;
        public static readonly int SILABS_CP2110 = 0xea80;

        public static readonly int VENDOR_PROLIFIC = 0x067b;
        public static readonly int PROLIFIC_PL2303 = 0x2303;

        public static readonly int VENDOR_QINHENG = 0x1a86;
        public static readonly int QINHENG_HL340 = 0x7523;

        public static readonly int VENDOR_ELATEC = 0x09D8;
        public static readonly int ELATEC_TWN3_KEYBOARD = 0x0310;    // Not needed
        public static readonly int ELATEC_TWN3_CDC = 0x0320;
        public static readonly int ELATEC_TWN4_MIFARE_NFC = 0x0406;  // One off for an Elatec customer
        public static readonly int ELATEC_TWN4_KEYBOARD = 0x0410;    // Not needed
        public static readonly int ELATEC_TWN4_CDC = 0x0420;
        public static readonly int ELATEC_TWN4_SC_READER = 0x0428;   // Uses CCID protocol, not serial

    }
}