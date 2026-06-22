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
        var searchLeft = taskbarRectangle.left;
        var searchRight = ResolveNotificationLeft(taskbarRectangle);
        if (TryGetTaskbarButtonsSearchRectangle(taskbarRectangle, rowRectangle, searchLeft, searchRight, out var taskbarButtonsSearchRectangle)) await taskbarButtonReader.RefreshAsync(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle);
    }

    public TaskbarLayoutSnapshot Calculate(double requestedWidth, double requestedHeight, double scaleFactor)
    {
        if (!taskbarWindowLocator.TryRefresh()) return default;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return default;
        scaleFactor = NormalizeScaleFactor(scaleFactor);

        var rowRectangle = TryGetWindowRectangle(taskbarWindowLocator.RebarWindow, out var rebarRectangle) ? rebarRectangle : taskbarRectangle;
        var searchLeft = taskbarRectangle.left;
        var searchRight = ResolveNotificationLeft(taskbarRectangle);

        TaskbarButtonGeometry geometry = default;
        if (options.TrackTaskbarButtons && TryGetTaskbarButtonsSearchRectangle(taskbarRectangle, rowRectangle, searchLeft, searchRight, out var taskbarButtonsSearchRectangle)) taskbarButtonReader.TryGetTaskbarButtonGeometry(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle, out geometry);

        var alignment = TaskbarAlignmentDetector.Detect(taskbarWindowLocator.TaskbarWindow, geometry.StartButton);
        var (areaLeft, areaRight) = SelectContentArea(options.Placement, alignment, taskbarRectangle, searchRight, geometry);

        var availableWidth = Math.Max(0, areaRight - areaLeft);
        if (availableWidth <= 0) return new TaskbarLayoutSnapshot(0, 0, 0, 0, 0, scaleFactor, false);

        var requestedWidthInPixels = Math.Max(1, (int)Math.Ceiling(requestedWidth * scaleFactor));
        var requestedHeightInPixels = Math.Max(1, (int)Math.Ceiling(requestedHeight * scaleFactor));
        var width = Math.Min(requestedWidthInPixels, availableWidth);
        var height = Math.Min(requestedHeightInPixels, Math.Max(1, rowRectangle.bottom - rowRectangle.top));
        var x = areaRight - width - taskbarRectangle.left;
        var y = rowRectangle.top - taskbarRectangle.top;

        return new TaskbarLayoutSnapshot(x, y, width, height, availableWidth, scaleFactor, true);
    }

    private int ResolveNotificationLeft(RECT taskbarRectangle)
    {
        if (options.TrackNotificationArea && TryGetWindowRectangle(taskbarWindowLocator.NotificationWindow, out var notificationRectangle)) return Math.Min(taskbarRectangle.right, notificationRectangle.left);
        return taskbarRectangle.right;
    }

    private static (int AreaLeft, int AreaRight) SelectContentArea(TaskbarContentPlacement placement, TaskbarAlignment alignment, RECT taskbarRectangle, int notificationLeft, TaskbarButtonGeometry geometry)
    {
        var leftGap = ComputeLeftGap(taskbarRectangle, geometry);
        var rightGap = ComputeRightGap(notificationLeft, geometry);
        var leftAvailableWidth = Math.Max(0, leftGap.Right - leftGap.Left);
        var rightAvailableWidth = Math.Max(0, rightGap.Right - rightGap.Left);

        var useLeftGap = placement switch
        {
            TaskbarContentPlacement.BeforeStartButton => alignment == TaskbarAlignment.Center,
            TaskbarContentPlacement.Auto => alignment == TaskbarAlignment.Center && leftAvailableWidth > rightAvailableWidth,
            _ => false
        };

        return useLeftGap ? leftGap : rightGap;
    }

    private static (int Left, int Right) ComputeLeftGap(RECT taskbarRectangle, TaskbarButtonGeometry geometry)
    {
        var left = geometry.WidgetsButton.IsValid ? geometry.WidgetsButton.Right : taskbarRectangle.left;
        var right = geometry.StartButton.IsValid ? geometry.StartButton.Left : taskbarRectangle.left;
        return (left, right);
    }

    private static (int Left, int Right) ComputeRightGap(int notificationLeft, TaskbarButtonGeometry geometry)
    {
        var left = geometry.TaskbarButtonsGroup.IsValid ? geometry.TaskbarButtonsGroup.Right : (geometry.StartButton.IsValid ? geometry.StartButton.Right : notificationLeft);
        var right = notificationLeft;
        if (geometry.WidgetsButton.IsValid && geometry.WidgetsButton.Left >= left && geometry.WidgetsButton.Left < right) right = geometry.WidgetsButton.Left;
        return (left, right);
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
