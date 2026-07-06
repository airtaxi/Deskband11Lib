using System.Runtime.Versioning;
using Deskband11Lib.Core.Internal;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core;

[SupportedOSPlatform("windows10.0.22000.0")]
public static class TaskbarMonitor
{
    public static IReadOnlyList<int> GetAvailableMonitorIdentities()
    {
        var identities = new List<int>();

        var primaryWindow = PInvoke.FindWindow(TaskbarWindowLocator.PrimaryTaskbarClassName, null);
        if (!primaryWindow.IsNull) identities.Add(0);

        var secondaryWindows = new List<HWND>();
        var previousWindow = HWND.Null;
        while (true)
        {
            previousWindow = PInvoke.FindWindowEx(HWND.Null, previousWindow, TaskbarWindowLocator.SecondaryTaskbarClassName, null);
            if (previousWindow.IsNull) break;
            secondaryWindows.Add(previousWindow);
        }

        var orderedSecondaryIdentities = secondaryWindows
            .OrderBy(window => PInvoke.GetWindowRect(window, out var rectangle) ? rectangle.left : int.MaxValue)
            .Select((_, index) => index + 1)
            .ToList();

        identities.AddRange(orderedSecondaryIdentities);
        return identities;
    }
}