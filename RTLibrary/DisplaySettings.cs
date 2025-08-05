#if !WINDOWS7
using System;
using System.Runtime.InteropServices;
#endif
using System.Windows.Forms;

namespace RTLibrary
{
    public static class DisplaySettings
    {
        public static double DisplayScale(Screen screen)
        {
#if WINDOWS7
            return 1D;
#else
            uint x, y;
            DisplaySettings.GetDpi(screen, DisplaySettings.DpiType.Effective, out x, out y);
            double eff = x;
            if (screen.Primary)
                return eff / 96D;
            else
            {
                DisplaySettings.GetDpi(screen, DisplaySettings.DpiType.Raw, out x, out y);
                double raw = x;
                return 0.25 * Math.Round(4D * eff / raw);
            }
#endif
        }

#if !WINDOWS7
        public static void GetDpi(Screen screen, DpiType dpiType, out uint dpiX, out uint dpiY)
        {
            var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var mon = MonitorFromPoint(pnt, 2/*MONITOR_DEFAULTTONEAREST*/);
            GetDpiForMonitor(mon, dpiType, out dpiX, out dpiY);
        }

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmonitor"></param>
        /// <param name="dpiType"></param>
        /// <param name="dpiX"></param>
        /// <param name="dpiY"></param>
        /// <returns></returns>
        /// <see href="https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx"/>
        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280511(v=vs.85).aspx
        public enum DpiType
        {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }
#endif

    }
}
