namespace Deskband11Lib.Native.Internal;

internal readonly record struct PendingNativeHostEvent(NativeHostEvent Event, string? ErrorMessage);
