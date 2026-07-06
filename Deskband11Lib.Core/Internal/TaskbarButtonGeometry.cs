namespace Deskband11Lib.Core.Internal;

internal readonly record struct ButtonSpan(int Left, int Right)
{
    public static ButtonSpan Invalid => new(0, 0);

    public bool IsValid => Right > Left;

    public int Width => Math.Max(0, Right - Left);
}

internal readonly record struct TaskbarButtonGeometry(ButtonSpan StartButton, ButtonSpan WidgetsButton, ButtonSpan TaskbarButtonsGroup, ButtonSpan NotificationArea);
