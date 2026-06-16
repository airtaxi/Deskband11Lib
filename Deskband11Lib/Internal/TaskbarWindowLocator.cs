using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Internal;

internal sealed class TaskbarWindowLocator
{
    public HWND TaskbarWindow { get; private set; }

    public HWND RebarWindow { get; private set; }

    public HWND TaskSwitchWindow { get; private set; }

    public HWND TaskListWindow { get; private set; }

    public HWND NotificationWindow { get; private set; }

    public bool TryRefresh()
    {
        TaskbarWindow = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (TaskbarWindow.IsNull) return false;

        RebarWindow = PInvoke.FindWindowEx(TaskbarWindow, HWND.Null, "ReBarWindow32", null);
        TaskSwitchWindow = RebarWindow.IsNull ? HWND.Null : PInvoke.FindWindowEx(RebarWindow, HWND.Null, "MSTaskSwWClass", null);
        TaskListWindow = TaskSwitchWindow.IsNull ? HWND.Null : PInvoke.FindWindowEx(TaskSwitchWindow, HWND.Null, "MSTaskListWClass", null);
        NotificationWindow = PInvoke.FindWindowEx(TaskbarWindow, HWND.Null, "TrayNotifyWnd", null);
        return true;
    }
}
