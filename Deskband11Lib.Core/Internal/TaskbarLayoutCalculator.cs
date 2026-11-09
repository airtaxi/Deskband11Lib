using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

// Widths are measured along the taskbar and heights across it, so the same rules serve horizontal and vertical taskbars.
// Only the final snapshot is converted back to screen axes.
internal sealed class TaskbarLayoutCalculator(TaskbarWindowLocator taskbarWindowLocator, TaskbarContentHostOptions options, TaskbarButtonReader taskbarButtonReader, Func<int> getEffectiveMonitorIdentity)
{
    private static double? s_lastScaleFactor;

    public async Task RefreshTaskbarButtonMeasurementAsync()
    {
        if (!options.TrackTaskbarButtons) return;
        if (!taskbarWindowLocator.TryRefresh(getEffectiveMonitorIdentity())) return;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return;

        var isVertical = TaskbarEdgeDetector.IsVertical(TaskbarEdgeDetector.Detect(taskbarWindowLocator.TaskbarWindow, taskbarRectangle));
        var taskbarAxisRectangle = TaskbarAxisRectangle.FromRectangle(taskbarRectangle, isVertical);
        var rowAxisRectangle = ResolveRowAxisRectangle(taskbarAxisRectangle, isVertical);
        var searchEnd = ResolveNotificationStart(taskbarAxisRectangle, isVertical);
        if (TryGetTaskbarButtonsSearchRectangle(taskbarAxisRectangle, rowAxisRectangle, searchEnd, isVertical, out var taskbarButtonsSearchRectangle)) await taskbarButtonReader.RefreshAsync(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle, isVertical);
    }

    public TaskbarLayoutSnapshot Calculate(double requestedWidth, double requestedHeight, double scaleFactor, TaskbarSlotInfo? ownSlot, IReadOnlyList<TaskbarSlotInfo> siblingSlots)
    {
        if (!taskbarWindowLocator.TryRefresh(getEffectiveMonitorIdentity())) return default;
        if (!TryGetWindowRectangle(taskbarWindowLocator.TaskbarWindow, out var taskbarRectangle)) return default;
        scaleFactor = NormalizeScaleFactor(scaleFactor);

        var edge = TaskbarEdgeDetector.Detect(taskbarWindowLocator.TaskbarWindow, taskbarRectangle);
        var isVertical = TaskbarEdgeDetector.IsVertical(edge);
        var taskbarAxisRectangle = TaskbarAxisRectangle.FromRectangle(taskbarRectangle, isVertical);
        var rowAxisRectangle = ResolveRowAxisRectangle(taskbarAxisRectangle, isVertical);
        var searchEnd = ResolveNotificationStart(taskbarAxisRectangle, isVertical);

        TaskbarButtonGeometry geometry = default;
        if (options.TrackTaskbarButtons && TryGetTaskbarButtonsSearchRectangle(taskbarAxisRectangle, rowAxisRectangle, searchEnd, isVertical, out var taskbarButtonsSearchRectangle)) taskbarButtonReader.TryGetTaskbarButtonGeometry(taskbarWindowLocator.TaskbarWindow, taskbarButtonsSearchRectangle, isVertical, out geometry);

        var alignment = TaskbarAlignmentDetector.Detect(taskbarAxisRectangle, geometry.StartButton);
        var notificationStart = geometry.NotificationArea.IsValid ? geometry.NotificationArea.Start : searchEnd;
        var startGap = ComputeStartGap(taskbarAxisRectangle, geometry);
        var endGap = ComputeEndGap(notificationStart, geometry);
        var (areaStart, areaEnd, alignToAreaStart) = SelectContentArea(options.Placement, alignment, startGap, endGap);
        var resolvedPlacement = ResolveActualPlacement(options.Placement, alignment, startGap.Length, endGap.Length);

        var availableWidth = Math.Max(0, areaEnd - areaStart);
        if (availableWidth <= 0) return new TaskbarLayoutSnapshot(0, 0, 0, 0, 0, scaleFactor, false, resolvedPlacement, edge);

        var requestedHeightInPixels = Math.Max(1, (int)Math.Ceiling(requestedHeight * scaleFactor));
        var height = Math.Min(requestedHeightInPixels, Math.Max(1, rowAxisRectangle.CrossLength));

        var effectiveOwnSlot = ownSlot.HasValue && ownSlot.Value.ActualPlacement != resolvedPlacement ? ownSlot.Value with { ActualPlacement = resolvedPlacement } : ownSlot;
        var siblingsInSameArea = FilterSiblingsInSameArea(siblingSlots, resolvedPlacement, getEffectiveMonitorIdentity());

        double allocatedWidth;
        double offset;

        if (effectiveOwnSlot.HasValue && siblingsInSameArea.Count > 0)
        {
            var allocation = AllocateWidth(effectiveOwnSlot.Value, siblingsInSameArea, availableWidth, scaleFactor);
            allocatedWidth = allocation.Width;
            offset = allocation.Offset;
        }
        else
        {
            var requestedWidthInPixels = Math.Max(1, (int)Math.Ceiling(requestedWidth * scaleFactor));
            allocatedWidth = Math.Min(requestedWidthInPixels, availableWidth);
            offset = 0;
        }

        if (allocatedWidth <= 0) return new TaskbarLayoutSnapshot(0, 0, 0, 0, 0, scaleFactor, false, resolvedPlacement, edge);

        var mainPosition = alignToAreaStart ? areaStart + offset : areaEnd - offset - allocatedWidth;
        return CreateLayoutSnapshot(taskbarRectangle, isVertical, mainPosition, allocatedWidth, rowAxisRectangle.CrossStart, height, availableWidth, scaleFactor, resolvedPlacement, edge);
    }

    // The hosted window is a child of the taskbar window, so its position is relative to the taskbar rectangle.
    // On a vertical taskbar the content cannot grow wider than its allocated height, so that height is also the maximum width.
    private static TaskbarLayoutSnapshot CreateLayoutSnapshot(RECT taskbarRectangle, bool isVertical, double mainPosition, double width, int crossPosition, int height, int availableWidth, double scaleFactor, TaskbarContentPlacement resolvedPlacement, TaskbarEdge edge)
    {
        if (isVertical) return new TaskbarLayoutSnapshot(crossPosition - taskbarRectangle.left, mainPosition - taskbarRectangle.top, height, width, height, scaleFactor, true, resolvedPlacement, edge);
        return new TaskbarLayoutSnapshot(mainPosition - taskbarRectangle.left, crossPosition - taskbarRectangle.top, width, height, availableWidth, scaleFactor, true, resolvedPlacement, edge);
    }

    private static TaskbarContentPlacement ResolveActualPlacement(TaskbarContentPlacement requestedPlacement, TaskbarAlignment alignment, int startGapLength, int endGapLength)
    {
        return requestedPlacement switch
        {
            TaskbarContentPlacement.LeftEdge => alignment == TaskbarAlignment.Center ? TaskbarContentPlacement.LeftEdge : TaskbarContentPlacement.BeforeNotificationArea,
            TaskbarContentPlacement.BeforeStartButton => alignment == TaskbarAlignment.Center ? TaskbarContentPlacement.BeforeStartButton : TaskbarContentPlacement.BeforeNotificationArea,
            TaskbarContentPlacement.Auto => startGapLength > endGapLength ? TaskbarContentPlacement.LeftEdge : TaskbarContentPlacement.BeforeNotificationArea,
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

    // The rebar window collapses to an empty rectangle when the taskbar has no app buttons (no pinned or running apps), so the taskbar rectangle is used instead.
    // Only the cross axis of the rebar is used, because its extent along the taskbar does not match the app buttons.
    private TaskbarAxisRectangle ResolveRowAxisRectangle(TaskbarAxisRectangle taskbarAxisRectangle, bool isVertical)
    {
        if (!TryGetWindowRectangle(taskbarWindowLocator.RebarWindow, out var rebarRectangle)) return taskbarAxisRectangle;

        var rebarAxisRectangle = TaskbarAxisRectangle.FromRectangle(rebarRectangle, isVertical);
        return rebarAxisRectangle.CrossLength > 0 ? rebarAxisRectangle : taskbarAxisRectangle;
    }

    private int ResolveNotificationStart(TaskbarAxisRectangle taskbarAxisRectangle, bool isVertical)
    {
        if (options.TrackNotificationArea && TryGetWindowRectangle(taskbarWindowLocator.NotificationWindow, out var notificationRectangle)) return Math.Min(taskbarAxisRectangle.MainEnd, TaskbarAxisRectangle.FromRectangle(notificationRectangle, isVertical).MainStart);
        return taskbarAxisRectangle.MainEnd;
    }

    private static (int AreaStart, int AreaEnd, bool AlignToAreaStart) SelectContentArea(TaskbarContentPlacement placement, TaskbarAlignment alignment, AxisSpan startGap, AxisSpan endGap)
    {
        var useStartGap = placement switch
        {
            TaskbarContentPlacement.LeftEdge => alignment == TaskbarAlignment.Center,
            TaskbarContentPlacement.BeforeStartButton => alignment == TaskbarAlignment.Center,
            TaskbarContentPlacement.Auto => alignment == TaskbarAlignment.Center && startGap.Length > endGap.Length,
            _ => false
        };

        if (!useStartGap) return (endGap.Start, endGap.End, false);

        var alignToAreaStart = placement is TaskbarContentPlacement.LeftEdge or TaskbarContentPlacement.Auto;
        return (startGap.Start, startGap.End, alignToAreaStart);
    }

    private static AxisSpan ComputeStartGap(TaskbarAxisRectangle taskbarAxisRectangle, TaskbarButtonGeometry geometry) => new(geometry.WidgetsButton.IsValid ? geometry.WidgetsButton.End : taskbarAxisRectangle.MainStart, geometry.StartButton.IsValid ? geometry.StartButton.Start : taskbarAxisRectangle.MainStart);

    private static AxisSpan ComputeEndGap(int notificationStart, TaskbarButtonGeometry geometry)
    {
        var start = geometry.TaskbarButtonsGroup.IsValid ? geometry.TaskbarButtonsGroup.End : (geometry.StartButton.IsValid ? geometry.StartButton.End : notificationStart);
        var end = notificationStart;
        if (geometry.WidgetsButton.IsValid && geometry.WidgetsButton.Start >= start && geometry.WidgetsButton.Start < end) end = geometry.WidgetsButton.Start;
        return new AxisSpan(start, end);
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

    private static bool TryGetTaskbarButtonsSearchRectangle(TaskbarAxisRectangle taskbarAxisRectangle, TaskbarAxisRectangle rowAxisRectangle, int searchEnd, bool isVertical, out RECT rectangle)
    {
        var searchAxisRectangle = new TaskbarAxisRectangle(taskbarAxisRectangle.MainStart, searchEnd, Math.Max(taskbarAxisRectangle.CrossStart, rowAxisRectangle.CrossStart), Math.Min(taskbarAxisRectangle.CrossEnd, rowAxisRectangle.CrossEnd));
        rectangle = searchAxisRectangle.ToRectangle(isVertical);
        return !searchAxisRectangle.IsEmpty;
    }
}
