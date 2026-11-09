using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Deskband11Lib.Core.Internal;

// The edge is derived from the taskbar window itself rather than from settings, so each taskbar on each monitor reports its own position.
internal static unsafe class TaskbarEdgeDetector
{
    public static TaskbarEdge Detect(HWND taskbarWindow, RECT taskbarRectangle)
    {
        var width = taskbarRectangle.right - taskbarRectangle.left;
        var height = taskbarRectangle.bottom - taskbarRectangle.top;
        if (width <= 0 || height <= 0) return TaskbarEdge.Unknown;

        var isVertical = height > width;
        if (!TryGetMonitorRectangle(taskbarWindow, out var monitorRectangle)) return isVertical ? TaskbarEdge.Left : TaskbarEdge.Bottom;
        if (isVertical) return taskbarRectangle.left - monitorRectangle.left <= monitorRectangle.right - taskbarRectangle.right ? TaskbarEdge.Left : TaskbarEdge.Right;
        return taskbarRectangle.top - monitorRectangle.top <= monitorRectangle.bottom - taskbarRectangle.bottom ? TaskbarEdge.Top : TaskbarEdge.Bottom;
    }

    public static bool IsVertical(TaskbarEdge edge) => edge is TaskbarEdge.Left or TaskbarEdge.Right;

    private static bool TryGetMonitorRectangle(HWND window, out RECT monitorRectangle)
    {
        monitorRectangle = default;
        if (window.IsNull) return false;

        var monitorHandle = PInvoke.MonitorFromWindow(window, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (monitorHandle.IsNull) return false;

        var monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!PInvoke.GetMonitorInfo(monitorHandle, &monitorInfo)) return false;

        monitorRectangle = monitorInfo.rcMonitor;
        return true;
    }
}
