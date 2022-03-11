/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

namespace Hoho.Android.UsbSerial.Driver
{
    /// <summary>
    /// Fixes Xamarin bug with missing constants
    /// https://forums.xamarin.com/discussion/comment/197948/#Comment_197948
    /// Filed as https://bugzilla.xamarin.com/show_bug.cgi?id=47663
    /// </summary>
    public class UsbSupport : Java.Lang.Object
    {
        public const int UsbClassAppSpec = 254;
        public const int UsbClassAudio = 1;
        public const int UsbClassCdcData = 10;
        public const int UsbClassComm = 2;
        public const int UsbClassContentSec = 13;
        public const int UsbClassCscid = 11;
        public const int UsbClassHid = 3;
        public const int UsbClassHub = 9;
        public const int UsbClassMassStorage = 8;
        public const int UsbClassMisc = 239;
        public const int UsbClassPerInterface = 0;
        public const int UsbClassPhysica = 5;
        public const int UsbClassPrinter = 7;
        public const int UsbClassStillImage = 6;
        public const int UsbClassVendorSpec = 255;
        public const int UsbClassVideo = 14;
        public const int UsbClassWirelessController = 234;
        public const int UsbDirOut = 0;
        public const int UsbDirIn = 128;
        public const int UsbEndpointDirMask = 128;
        public const int UsbEndpointNumberMask = 15;
        public const int UsbEndpointXferBulk = 2;
        public const int UsbEndpointXferControl = 0;
        public const int UsbEndpointXferInt = 3;
        public const int UsbEndpointXferIsoc = 1;
    }
}