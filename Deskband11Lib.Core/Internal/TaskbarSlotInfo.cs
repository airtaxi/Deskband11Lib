namespace Deskband11Lib.Core.Internal;

internal readonly record struct TaskbarSlotInfo(ushort SlotIndex, ushort ManualSlotPriority, double PreferredWidth, TaskbarContentPlacement ActualPlacement, int MonitorIdentity, nint WindowHandle, bool AllowFixedSlotResize)
{
    public bool IsStretch => PreferredWidth == double.MaxValue;
}
