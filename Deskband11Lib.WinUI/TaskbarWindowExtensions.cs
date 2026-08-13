using Deskband11Lib.Core;
using Microsoft.UI.Xaml;
using System.Runtime.Versioning;

namespace Deskband11Lib.WinUI;

[SupportedOSPlatform("windows10.0.22000.0")]
public static class TaskbarWindowExtensions
{
    public static bool IsWindowAlive(this Window? window)
    {
        if (window is null) return false;

        try
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            return TaskbarWindowHelper.IsWindowAlive(windowHandle);
        }
        catch { return false; }
    }
}
