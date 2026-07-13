using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Deskband11Lib.Core.Internal;

internal static unsafe class MonitorRefreshRateProvider
{
    public static int GetRefreshRateHz(HWND windowHandle)
    {
        if (windowHandle.IsNull) return 0;

        var monitorHandle = PInvoke.MonitorFromWindow(windowHandle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (monitorHandle.IsNull) return 0;

        var monitorInfoEx = new MONITORINFOEXW { monitorInfo = { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        if (!PInvoke.GetMonitorInfo(monitorHandle, (MONITORINFO*)&monitorInfoEx)) return 0;

        var deviceName = monitorInfoEx.szDevice.ToString();
        if (string.IsNullOrEmpty(deviceName)) return 0;

        var devMode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        if (!PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref devMode)) return 0;

        var refreshRate = (int)devMode.dmDisplayFrequency;
        return refreshRate >= 2 ? refreshRate : 0;
    }
}