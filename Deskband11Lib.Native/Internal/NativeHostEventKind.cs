namespace Deskband11Lib.Native.Internal;

internal enum NativeHostEventKind
{
    None = 0,

    Attached = 1,

    AttachFailed = 2,

    TaskbarWindowRecreated = 3,

    TaskbarWindowDisappeared = 4,

    TaskbarDpiChanged = 5,

    TaskbarWindowRecreationRequired = 6,

    TaskbarEdgeChanged = 7,

    ContentBoundsChanged = 8,

    Error = 9
}
