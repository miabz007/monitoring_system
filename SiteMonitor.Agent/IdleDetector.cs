
using System.Runtime.InteropServices;

namespace SiteMonitor.Agent;

public class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(
        ref LASTINPUTINFO plii);

    public static double GetIdleSeconds()
    {
        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
            return 0;

        uint idleMilliseconds =
            unchecked((uint)Environment.TickCount - info.dwTime);

        return idleMilliseconds / 1000.0;
    }
}
