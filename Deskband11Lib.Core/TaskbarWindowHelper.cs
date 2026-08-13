using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core;

[SupportedOSPlatform("windows10.0.22000.0")]
public static class TaskbarWindowHelper
{
    public static bool IsWindowAlive(nint windowHandle)
    {
        if (windowHandle == 0) return false;
        return PInvoke.IsWindow(new HWND(windowHandle));
    }
}
