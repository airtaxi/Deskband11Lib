using Deskband11Lib.Core;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;

namespace Deskband11Lib.Wpf;

[SupportedOSPlatform("windows10.0.22000.0")]
public static class TaskbarWindowExtensions
{
    public static bool IsWindowAlive(this Window? window)
    {
        if (window is null) return false;

        try
        {
            var windowHandle = new WindowInteropHelper(window).Handle;
            return TaskbarWindowHelper.IsWindowAlive(windowHandle);
        }
        catch { return false; }
    }
}
