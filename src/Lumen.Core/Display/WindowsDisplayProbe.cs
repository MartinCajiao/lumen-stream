using System.Runtime.InteropServices;
using Lumen.Core.Display;

namespace Lumen.Core.Display;

public sealed class WindowsDisplayProbe : IDisplayProbe
{
    public DisplayInfo Primary
    {
        get
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DisplayInfo.Fallback;
            }

            var mode = new DevMode();
            mode.dmSize = (short)Marshal.SizeOf<DevMode>();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
            {
                return DisplayInfo.Fallback;
            }

            var hz = mode.dmDisplayFrequency > 1 ? mode.dmDisplayFrequency : 60;
            var width = mode.dmPelsWidth > 0 ? mode.dmPelsWidth : 1920;
            var height = mode.dmPelsHeight > 0 ? mode.dmPelsHeight : 1080;
            return new DisplayInfo(width, height, hz, mode.dmDeviceName ?? "DISPLAY");
        }
    }

    private const int EnumCurrentSettings = -1;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DevMode
    {
        private const int CchDeviceName = 32;
        private const int CchFormName = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
    }
}
