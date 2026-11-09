namespace Deskband11Lib.Native.Internal;

internal enum NativeResult
{
    Ok = 0,

    NoEvent = 1,

    InvalidArgument = -1,

    InvalidHandle = -2,

    WrongThread = -3,

    InvalidState = -4,

    UnsupportedDpiAwareness = -5,

    Win32Error = -6,

    Unexpected = -100
}
