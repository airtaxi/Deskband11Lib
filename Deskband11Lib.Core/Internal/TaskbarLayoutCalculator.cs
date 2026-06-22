using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed class TaskbarLayoutCalculator(TaskbarWindowLocator taskbarWindowLocator, TaskbarContentHostOptions options, TaskbarButtonReader taskbarButtonReader)
{
    private static double? s_lastScaleFactor;

    public async Task RefreshTaskbarButtonMeasurementAsync(double scaleFactor)
    {
        if (!options.TrackTaskbarButtons) return;
        if (!taskbarWindowLocator.TryRefresh()) return;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return;
        scaleFactor = NormalizeScaleFactor(scaleFactor);

        var rowRectangle = TryGetWindowRectangle(taskbarWindowLocator.RebarWindow, out var rebarRectangle) ? rebarRectangle : taskbarRectangle;
        var leftBoundary = taskbarRectangle.left;
        var rightBoundary = taskbarRectangle.right;
        if (options.TrackNotificationArea && TryGetWindowRectangle(taskbarWindowLocator.NotificationWindow, out var notificationRectangle)) rightBoundary = Math.Min(rightBoundary, notificationRectangle.left);
        if (TryGetTaskbarButtonsSearchRectangle(taskbarRectangle, rowRectangle, leftBoundary, rightBoundary, out var taskbarButtonsSearchRectangle)) await taskbarButtonReader.RefreshAsync(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle);
    }

    public TaskbarLayoutSnapshot Calculate(double requestedWidth, double requestedHeight, double scaleFactor)
    {
        if (!taskbarWindowLocator.TryRefresh()) return default;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return default;
        scaleFactor = NormalizeScaleFactor(scaleFactor);

        var rowRectangle = TryGetWindowRectangle(taskbarWindowLocator.RebarWindow, out var rebarRectangle) ? rebarRectangle : taskbarRectangle;
        var leftBoundary = taskbarRectangle.left;
        var rightBoundary = taskbarRectangle.right;

        if (options.TrackNotificationArea && TryGetWindowRectangle(taskbarWindowLocator.NotificationWindow, out var notificationRectangle)) rightBoundary = Math.Min(rightBoundary, notificationRectangle.left);
        if (TryGetTaskbarButtonsSearchRectangle(taskbarRectangle, rowRectangle, leftBoundary, rightBoundary, out var taskbarButtonsSearchRectangle))
        {
            if (taskbarButtonReader.TryGetStartButtonRightEdge(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle, out var startButtonRightEdge)) leftBoundary = Math.Max(leftBoundary, startButtonRightEdge);
            if (options.TrackTaskbarButtons && taskbarButtonReader.TryGetTaskbarButtonsRightEdge(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle, out var taskbarButtonsRightEdge)) leftBoundary = Math.Max(leftBoundary, taskbarButtonsRightEdge);
        }

        var availableWidth = Math.Max(0, rightBoundary - leftBoundary);
        if (availableWidth <= 0) return new TaskbarLayoutSnapshot(0, 0, 0, 0, 0, scaleFactor, false);

        var requestedWidthInPixels = Math.Max(1, (int)Math.Ceiling(requestedWidth * scaleFactor));
        var requestedHeightInPixels = Math.Max(1, (int)Math.Ceiling(requestedHeight * scaleFactor));
        var width = Math.Min(requestedWidthInPixels, availableWidth);
        var height = Math.Min(requestedHeightInPixels, Math.Max(1, rowRectangle.bottom - rowRectangle.top));
        var x = options.Placement == TaskbarContentPlacement.BeforeNotificationArea ? rightBoundary - width - taskbarRectangle.left : leftBoundary - taskbarRectangle.left;
        var y = rowRectangle.top - taskbarRectangle.top;

        return new TaskbarLayoutSnapshot(x, y, width, height, availableWidth, scaleFactor, true);
    }

    private static double NormalizeScaleFactor(double scaleFactor)
    {
        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0) return s_lastScaleFactor ?? 1.0;

        s_lastScaleFactor = scaleFactor;
        return scaleFactor;
    }

    private static bool TryGetWindowRectangle(HWND window, out RECT rectangle)
    {
        rectangle = default;
        return !window.IsNull && PInvoke.GetWindowRect(window, out rectangle);
    }

    private static bool TryGetTaskbarButtonsSearchRectangle(RECT taskbarRectangle, RECT rowRectangle, int leftBoundary, int rightBoundary, out RECT rectangle)
    {
        rectangle = new RECT
        {
            left = leftBoundary,
            top = Math.Max(taskbarRectangle.top, rowRectangle.top),
            right = rightBoundary,
            bottom = Math.Min(taskbarRectangle.bottom, rowRectangle.bottom)
        };

        return rectangle.right > rectangle.left && rectangle.bottom > rectangle.top;
    }
}
