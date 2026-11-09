/// Where the window is placed along the taskbar.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum TaskbarContentPlacement {
    /// On a centered taskbar, the more spacious gap around the Start button group. Otherwise next to the notification area.
    #[default]
    Auto = 0,
    /// On a centered taskbar, the far end after the Widgets button. Otherwise next to the notification area.
    LeftEdge = 1,
    /// Next to the notification area.
    BeforeNotificationArea = 2,
    /// On a centered taskbar, next to the Start button. Otherwise next to the notification area.
    BeforeStartButton = 3,
}

/// Easing of the layout animation.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum Easing {
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
    #[default]
    CircleOut = 20,
    CircleInOut = 21,
}

/// The screen edge the taskbar sits on.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum TaskbarEdge {
    /// Not known until the first layout.
    #[default]
    Unknown = 0,
    Left = 1,
    Top = 2,
    Right = 3,
    Bottom = 4,
}

impl TaskbarEdge {
    /// Returns whether the taskbar sits on the left or right edge, where content is laid out from top to bottom.
    pub fn is_vertical(self) -> bool {
        matches!(self, Self::Left | Self::Right)
    }

    pub(crate) fn from_native(value: i32) -> Self {
        match value {
            1 => Self::Left,
            2 => Self::Top,
            3 => Self::Right,
            4 => Self::Bottom,
            _ => Self::Unknown,
        }
    }
}

/// How the taskbar buttons are aligned. On a left or right taskbar, `Left` means aligned to the top.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum TaskbarAlignment {
    #[default]
    Unknown = 0,
    Left = 1,
    Center = 2,
}

impl TaskbarAlignment {
    pub(crate) fn from_native(value: i32) -> Self {
        match value {
            1 => Self::Left,
            2 => Self::Center,
            _ => Self::Unknown,
        }
    }
}

/// The kind of failure reported by the native library.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
#[non_exhaustive]
pub enum ResultCode {
    InvalidArgument,
    InvalidHandle,
    WrongThread,
    InvalidState,
    UnsupportedDpiAwareness,
    Win32Error,
    Unexpected,
    /// A result code added by a newer version of the native library.
    Other(i32),
}

impl ResultCode {
    pub(crate) fn from_native(value: i32) -> Self {
        match value {
            -1 => Self::InvalidArgument,
            -2 => Self::InvalidHandle,
            -3 => Self::WrongThread,
            -4 => Self::InvalidState,
            -5 => Self::UnsupportedDpiAwareness,
            -6 => Self::Win32Error,
            -100 => Self::Unexpected,
            other => Self::Other(other),
        }
    }
}
