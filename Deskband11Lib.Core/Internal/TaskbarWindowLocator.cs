using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed class TaskbarWindowLocator
{
    public const string PrimaryTaskbarClassName = "Shell_TrayWnd";

    public const string SecondaryTaskbarClassName = "Shell_SecondaryTrayWnd";

    public HWND TaskbarWindow { get; private set; }

    public HWND RebarWindow { get; private set; }

    public HWND TaskSwitchWindow { get; private set; }

    public HWND TaskListWindow { get; private set; }

    public HWND NotificationWindow { get; private set; }

    public string TaskbarClassName { get; private set; } = PrimaryTaskbarClassName;

    public bool TryRefresh(int preferredMonitorIdentity = 0)
    {
        if (preferredMonitorIdentity <= 0)
        {
            TaskbarClassName = PrimaryTaskbarClassName;
            return TryRefreshPrimary();
        }

        TaskbarClassName = SecondaryTaskbarClassName;
        return TryRefreshSecondary(preferredMonitorIdentity);
    }

    private bool TryRefreshPrimary()
    {
        TaskbarWindow = PInvoke.FindWindow(PrimaryTaskbarClassName, null);
        if (TaskbarWindow.IsNull) return false;

        RebarWindow = PInvoke.FindWindowEx(TaskbarWindow, HWND.Null, "ReBarWindow32", null);
        TaskSwitchWindow = RebarWindow.IsNull ? HWND.Null : PInvoke.FindWindowEx(RebarWindow, HWND.Null, "MSTaskSwWClass", null);
        TaskListWindow = TaskSwitchWindow.IsNull ? HWND.Null : PInvoke.FindWindowEx(TaskSwitchWindow, HWND.Null, "MSTaskListWClass", null);
        NotificationWindow = PInvoke.FindWindowEx(TaskbarWindow, HWND.Null, "TrayNotifyWnd", null);
        return true;
    }

    private unsafe bool TryRefreshSecondary(int preferredMonitorIdentity)
    {
        TaskbarWindow = HWND.Null;
        RebarWindow = HWND.Null;
        TaskSwitchWindow = HWND.Null;
        TaskListWindow = HWND.Null;
        NotificationWindow = HWND.Null;

        var secondaryWindows = new List<HWND>();
        var previousWindow = HWND.Null;
        while (true)
        {
            previousWindow = PInvoke.FindWindowEx(HWND.Null, previousWindow, SecondaryTaskbarClassName, null);
            if (previousWindow.IsNull) break;
            secondaryWindows.Add(previousWindow);
        }

        if (secondaryWindows.Count == 0) return false;

        var orderedWindows = secondaryWindows
            .OrderBy(window => PInvoke.GetWindowRect(window, out var rectangle) ? rectangle.left : int.MaxValue)
            .ToList();

        var identityIndex = Math.Min(preferredMonitorIdentity - 1, orderedWindows.Count - 1);
        TaskbarWindow = orderedWindows[identityIndex];
        TaskListWindow = PInvoke.FindWindowEx(TaskbarWindow, HWND.Null, "MSTaskListWClass", null);
        return true;
    }
}