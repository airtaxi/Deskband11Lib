using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed class TaskbarLayoutCalculator(TaskbarWindowLocator taskbarWindowLocator, TaskbarContentHostOptions options, TaskbarButtonReader taskbarButtonReader, Func<int> getEffectiveMonitorIdentity)
{
    private static double? s_lastScaleFactor;

    public async Task RefreshTaskbarButtonMeasurementAsync()
    {
        if (!options.TrackTaskbarButtons) return;
        if (!taskbarWindowLocator.TryRefresh(getEffectiveMonitorIdentity())) return;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return;

        var rowRectangle = TryGetWindowRectangle(taskbarWindowLocator.RebarWindow, out var rebarRectangle) ? rebarRectangle : taskbarRectangle;
        var searchLeft = taskbarRectangle.left;
        var searchRight = ResolveNotificationLeft(taskbarRectangle);
        if (TryGetTaskbarButtonsSearchRectangle(taskbarRectangle, rowRectangle, searchLeft, searchRight, out var taskbarButtonsSearchRectangle)) await taskbarButtonReader.RefreshAsync(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle);
    }

    public TaskbarLayoutSnapshot Calculate(double requestedWidth, double requestedHeight, double scaleFactor, TaskbarSlotInfo? ownSlot, IReadOnlyList<TaskbarSlotInfo> siblingSlots)
    {
        if (!taskbarWindowLocator.TryRefresh(getEffectiveMonitorIdentity())) return default;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return default;
        scaleFactor = NormalizeScaleFactor(scaleFactor);

        var rowRectangle = TryGetWindowRectangle(taskbarWindowLocator.RebarWindow, out var rebarRectangle) ? rebarRectangle : taskbarRectangle;
        var searchLeft = taskbarRectangle.left;
        var searchRight = ResolveNotificationLeft(taskbarRectangle);

        TaskbarButtonGeometry geometry = default;
        if (options.TrackTaskbarButtons && TryGetTaskbarButtonsSearchRectangle(taskbarRectangle, rowRectangle, searchLeft, searchRight, out var taskbarButtonsSearchRectangle)) taskbarButtonReader.TryGetTaskbarButtonGeometry(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle, out geometry);

        var alignment = TaskbarAlignmentDetector.Detect(taskbarWindowLocator.TaskbarWindow, geometry.StartButton);
        var notificationLeft = geometry.NotificationArea.IsValid ? geometry.NotificationArea.Left : searchRight;
        var (areaLeft, areaRight, leftAlign) = SelectContentArea(options.Placement, alignment, taskbarRectangle, notificationLeft, geometry);

        var availableWidth = Math.Max(0, areaRight - areaLeft);

        var resolvedPlacement = ResolveActualPlacement(options.Placement, alignment, leftAvailableWidth: Math.Max(0, ComputeLeftGap(taskbarRectangle, geometry).Right - ComputeLeftGap(taskbarRectangle, geometry).Left), rightAvailableWidth: Math.Max(0, ComputeRightGap(notificationLeft, geometry).Right - ComputeRightGap(notificationLeft, geometry).Left));

        if (availableWidth <= 0) return new TaskbarLayoutSnapshot(0, 0, 0, 0, 0, scaleFactor, false, resolvedPlacement);

        var requestedHeightInPixels = Math.Max(1, (int)Math.Ceiling(requestedHeight * scaleFactor));
        var height = Math.Min(requestedHeightInPixels, Math.Max(1, rowRectangle.bottom - rowRectangle.top));

        var effectiveOwnSlot = ownSlot.HasValue && ownSlot.Value.ActualPlacement != resolvedPlacement ? ownSlot.Value with { ActualPlacement = resolvedPlacement } : ownSlot;
        var siblingsInSameArea = FilterSiblingsInSameArea(siblingSlots, resolvedPlacement, getEffectiveMonitorIdentity());

        double allocatedWidth;
        double offsetX;

        if (effectiveOwnSlot.HasValue && siblingsInSameArea.Count > 0)
        {
            var allocation = AllocateWidth(effectiveOwnSlot.Value, siblingsInSameArea, availableWidth, scaleFactor);
            allocatedWidth = allocation.Width;
            offsetX = allocation.Offset;
        }
        else
        {
            var requestedWidthInPixels = Math.Max(1, (int)Math.Ceiling(requestedWidth * scaleFactor));
            allocatedWidth = Math.Min(requestedWidthInPixels, availableWidth);
            offsetX = 0;
        }

        if (allocatedWidth <= 0) return new TaskbarLayoutSnapshot(0, 0, 0, 0, 0, scaleFactor, false, resolvedPlacement);

        var x = (leftAlign ? areaLeft + offsetX : areaRight - offsetX - allocatedWidth) - taskbarRectangle.left;
        var y = rowRectangle.top - taskbarRectangle.top;

        return new TaskbarLayoutSnapshot(x, y, allocatedWidth, height, availableWidth, scaleFactor, true, resolvedPlacement);
    }

    private static TaskbarContentPlacement ResolveActualPlacement(TaskbarContentPlacement requestedPlacement, TaskbarAlignment alignment, int leftAvailableWidth, int rightAvailableWidth)
    {
        return requestedPlacement switch
        {
            TaskbarContentPlacement.LeftEdge => alignment == TaskbarAlignment.Center ? TaskbarContentPlacement.LeftEdge : TaskbarContentPlacement.BeforeNotificationArea,
            TaskbarContentPlacement.BeforeStartButton => alignment == TaskbarAlignment.Center ? TaskbarContentPlacement.BeforeStartButton : TaskbarContentPlacement.BeforeNotificationArea,
            TaskbarContentPlacement.Auto => leftAvailableWidth > rightAvailableWidth ? TaskbarContentPlacement.LeftEdge : TaskbarContentPlacement.BeforeNotificationArea,
            _ => TaskbarContentPlacement.BeforeNotificationArea
        };
    }

    private static List<TaskbarSlotInfo> FilterSiblingsInSameArea(IReadOnlyList<TaskbarSlotInfo> allSlots, TaskbarContentPlacement resolvedPlacement, int monitorIdentity)
    {
        var result = new List<TaskbarSlotInfo>();
        foreach (var slot in allSlots)
        {
            if (slot.MonitorIdentity != monitorIdentity) continue;
            if (slot.ActualPlacement != resolvedPlacement) continue;
            result.Add(slot);
        }
        return result;
    }

    private static (double Width, double Offset) AllocateWidth(TaskbarSlotInfo ownSlot, List<TaskbarSlotInfo> siblings, int totalAvailableWidth, double scaleFactor)
    {
        var allSlots = new List<TaskbarSlotInfo>(siblings) { ownSlot };
        allSlots.Sort((a, b) => (a.ManualSlotPriority, a.SlotIndex).CompareTo((b.ManualSlotPriority, b.SlotIndex)));

        var nonResizableFixedSlots = allSlots.Where(slot => !slot.IsStretch && !slot.AllowFixedSlotResize).OrderBy(slot => slot.ManualSlotPriority).ThenBy(slot => slot.SlotIndex).ToList();
        var resizableFixedSlots = allSlots.Where(slot => !slot.IsStretch && slot.AllowFixedSlotResize).OrderBy(slot => slot.ManualSlotPriority).ThenBy(slot => slot.SlotIndex).ToList();
        var stretchSlots = allSlots.Where(slot => slot.IsStretch).OrderBy(slot => slot.ManualSlotPriority).ThenBy(slot => slot.SlotIndex).ToList();

        var allocations = new Dictionary<nint, int>();
        var allocatedWidthSum = 0;

        foreach (var slot in nonResizableFixedSlots)
        {
            var desiredWidth = (int)Math.Ceiling(slot.PreferredWidth * scaleFactor);
            var allocatedWidth = Math.Min(desiredWidth, Math.Max(0, totalAvailableWidth - allocatedWidthSum));
            allocations[slot.WindowHandle] = allocatedWidth;
            allocatedWidthSum += allocatedWidth;
        }

        var remainingForResizable = Math.Max(0, totalAvailableWidth - allocatedWidthSum);
        var resizableDesiredWidths = resizableFixedSlots.Select(slot => (int)Math.Ceiling(slot.PreferredWidth * scaleFactor)).ToList();
        var resizableDesiredWidthSum = resizableDesiredWidths.Sum();
        if (resizableFixedSlots.Count > 0)
        {
            if (remainingForResizable >= resizableDesiredWidthSum)
            {
                for (var index = 0; index < resizableFixedSlots.Count; index++)
                {
                    var allocatedWidth = resizableDesiredWidths[index];
                    allocations[resizableFixedSlots[index].WindowHandle] = allocatedWidth;
                    allocatedWidthSum += allocatedWidth;
                }
            }
            else allocatedWidthSum += AllocateProportionally(resizableFixedSlots, resizableDesiredWidths, remainingForResizable, allocations);
        }

        var remainingForStretch = Math.Max(0, totalAvailableWidth - allocatedWidthSum);
        var stretchWidth = stretchSlots.Count > 0 ? remainingForStretch / stretchSlots.Count : 0;
        foreach (var slot in stretchSlots) allocations[slot.WindowHandle] = stretchWidth;

        var offset = 0.0;
        foreach (var slot in allSlots)
        {
            if (slot.WindowHandle == ownSlot.WindowHandle) break;
            offset += allocations.GetValueOrDefault(slot.WindowHandle, 0);
        }

        return (allocations.GetValueOrDefault(ownSlot.WindowHandle, 0), offset);
    }

    private static int AllocateProportionally(List<TaskbarSlotInfo> slots, List<int> desiredWidths, int availableWidth, Dictionary<nint, int> allocations)
    {
        var desiredWidthSum = desiredWidths.Sum();
        if (desiredWidthSum <= 0)
        {
            for (var index = 0; index < slots.Count; index++) allocations[slots[index].WindowHandle] = 0;
            return 0;
        }

        var distributedWidth = 0;
        for (var index = 0; index < slots.Count; index++)
        {
            var allocatedWidth = (int)((long)availableWidth * desiredWidths[index] / desiredWidthSum);
            allocations[slots[index].WindowHandle] = allocatedWidth;
            distributedWidth += allocatedWidth;
        }

        var leftoverWidth = availableWidth - distributedWidth;
        for (var index = 0; index < slots.Count && leftoverWidth > 0; index++)
        {
            allocations[slots[index].WindowHandle]++;
            leftoverWidth--;
        }

        return availableWidth;
    }

    private int ResolveNotificationLeft(RECT taskbarRectangle)
    {
        if (options.TrackNotificationArea && TryGetWindowRectangle(taskbarWindowLocator.NotificationWindow, out var notificationRectangle)) return Math.Min(taskbarRectangle.right, notificationRectangle.left);
        return taskbarRectangle.right;
    }

    private static (int AreaLeft, int AreaRight, bool LeftAlign) SelectContentArea(TaskbarContentPlacement placement, TaskbarAlignment alignment, RECT taskbarRectangle, int notificationLeft, TaskbarButtonGeometry geometry)
    {
        var leftGap = ComputeLeftGap(taskbarRectangle, geometry);
        var rightGap = ComputeRightGap(notificationLeft, geometry);
        var leftAvailableWidth = Math.Max(0, leftGap.Right - leftGap.Left);
        var rightAvailableWidth = Math.Max(0, rightGap.Right - rightGap.Left);

        var useLeftGap = placement switch
        {
            TaskbarContentPlacement.LeftEdge => alignment == TaskbarAlignment.Center,
            TaskbarContentPlacement.BeforeStartButton => alignment == TaskbarAlignment.Center,
            TaskbarContentPlacement.Auto => alignment == TaskbarAlignment.Center && leftAvailableWidth > rightAvailableWidth,
            _ => false
        };

        if (!useLeftGap) return (rightGap.Left, rightGap.Right, false);

        var leftAlign = placement is TaskbarContentPlacement.LeftEdge or TaskbarContentPlacement.Auto;
        return (leftGap.Left, leftGap.Right, leftAlign);
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
