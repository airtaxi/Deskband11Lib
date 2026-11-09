export enum ResultCode {
    Ok = 0,
    NoEvent = 1,
    InvalidArgument = -1,
    InvalidHandle = -2,
    WrongThread = -3,
    InvalidState = -4,
    UnsupportedDpiAwareness = -5,
    Win32Error = -6,
    Unexpected = -100,
}

export enum TaskbarContentPlacement {
    Auto = 0,
    LeftEdge = 1,
    BeforeNotificationArea = 2,
    BeforeStartButton = 3,
}

export enum Easing {
    Linear = 0,
    SineIn = 1,
    SineOut = 2,
    SineInOut = 3,
    QuadraticIn = 4,
    QuadraticOut = 5,
    QuadraticInOut = 6,
    CubicIn = 7,
    CubicOut = 8,
    CubicInOut = 9,
    QuarticIn = 10,
    QuarticOut = 11,
    QuarticInOut = 12,
    QuinticIn = 13,
    QuinticOut = 14,
    QuinticInOut = 15,
    ExponentialIn = 16,
    ExponentialOut = 17,
    ExponentialInOut = 18,
    CircleIn = 19,
    CircleOut = 20,
    CircleInOut = 21,
}

export enum TaskbarEdge {
    Unknown = 0,
    Left = 1,
    Top = 2,
    Right = 3,
    Bottom = 4,
}

export enum TaskbarAlignment {
    Unknown = 0,
    Left = 1,
    Center = 2,
}

export enum HostEventKind {
    None = 0,
    Attached = 1,
    AttachFailed = 2,
    TaskbarWindowRecreated = 3,
    TaskbarWindowDisappeared = 4,
    TaskbarDpiChanged = 5,
    TaskbarWindowRecreationRequired = 6,
    TaskbarEdgeChanged = 7,
    ContentBoundsChanged = 8,
    Error = 9,
}

/** Returns whether the taskbar sits on the left or right edge, where content is laid out from top to bottom. */
export function isVerticalTaskbarEdge(edge: TaskbarEdge): boolean {
    return edge === TaskbarEdge.Left || edge === TaskbarEdge.Right;
}
