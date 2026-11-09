using Deskband11Lib.Core.Internal.GeneratedCom;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

// The main axis runs along the taskbar and the cross axis runs across it. The main axis is X on a horizontal taskbar and Y on a vertical one.
internal readonly record struct TaskbarAxisRectangle(int MainStart, int MainEnd, int CrossStart, int CrossEnd)
{
    public int MainLength => Math.Max(0, MainEnd - MainStart);

    public int CrossLength => Math.Max(0, CrossEnd - CrossStart);

    public bool IsEmpty => MainEnd <= MainStart || CrossEnd <= CrossStart;

    public AxisSpan MainSpan => new(MainStart, MainEnd);

    public static TaskbarAxisRectangle FromRectangle(RECT rectangle, bool isVertical) => isVertical ? new(rectangle.top, rectangle.bottom, rectangle.left, rectangle.right) : new(rectangle.left, rectangle.right, rectangle.top, rectangle.bottom);

    public static TaskbarAxisRectangle FromRectangle(GeneratedRectangle rectangle, bool isVertical) => isVertical ? new(rectangle.Top, rectangle.Bottom, rectangle.Left, rectangle.Right) : new(rectangle.Left, rectangle.Right, rectangle.Top, rectangle.Bottom);

    public RECT ToRectangle(bool isVertical) => isVertical ? new RECT { left = CrossStart, top = MainStart, right = CrossEnd, bottom = MainEnd } : new RECT { left = MainStart, top = CrossStart, right = MainEnd, bottom = CrossEnd };
}
