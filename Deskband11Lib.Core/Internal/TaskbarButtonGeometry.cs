namespace Deskband11Lib.Core.Internal;

// Every span is measured along the taskbar: horizontally on a bottom or top taskbar, vertically on a left or right taskbar.
internal readonly record struct TaskbarButtonGeometry(AxisSpan StartButton, AxisSpan WidgetsButton, AxisSpan TaskbarButtonsGroup, AxisSpan NotificationArea);
