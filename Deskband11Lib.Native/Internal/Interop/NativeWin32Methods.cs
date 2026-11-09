using System.Runtime.InteropServices;

namespace Deskband11Lib.Native.Internal.Interop;

internal static unsafe partial class NativeWin32Methods
{
    public const uint ApplicationMessageBase = 0x8000;
    public const uint Infinite = 0xFFFFFFFF;
    public const uint CreateWaitableTimerHighResolutionFlag = 0x00000002;
    public const uint TimerAllAccess = 0x001F0003;
    public const int PerMonitorDpiAwareness = 2;
    public const nint MessageOnlyParentWindow = -3;
    public const uint ModuleHandleUnchangedReferenceCountFlag = 0x00000002;
    public const uint ModuleHandleFromAddressFlag = 0x00000004;
    public const uint SetWindowPositionNoSize = 0x0001;
    public const uint SetWindowPositionNoMove = 0x0002;
    public const uint SetWindowPositionNoZOrder = 0x0004;
    public const uint SetWindowPositionNoActivate = 0x0010;
    public const uint SetWindowPositionFrameChanged = 0x0020;

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClassEx(WindowClassEx* windowClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
    public static partial nint CreateWindowEx(uint extendedStyle, char* className, char* windowName, uint style, int x, int y, int width, int height, nint parentWindowHandle, nint menuHandle, nint instanceHandle, void* parameter);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint windowHandle, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int DestroyWindow(nint windowHandle);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    public static partial int PostMessage(nint windowHandle, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int SetWindowPos(nint windowHandle, nint insertAfterWindowHandle, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint GetWindowDpiAwarenessContext(nint windowHandle);

    [LibraryImport("user32.dll")]
    public static partial int GetAwarenessFromDpiAwarenessContext(nint dpiAwarenessContext);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW", SetLastError = true)]
    public static partial int GetModuleHandleEx(uint flags, void* moduleName, nint* moduleHandle);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", SetLastError = true)]
    public static partial nint CreateEvent(void* securityAttributes, int manualReset, int initialState, char* name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial int SetEvent(nint eventHandle);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", SetLastError = true)]
    public static partial nint CreateWaitableTimerEx(void* securityAttributes, char* name, uint flags, uint desiredAccess);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial int SetWaitableTimer(nint timerHandle, long* dueTime, int period, nint completionRoutine, void* completionRoutineArgument, int resume);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial int CancelWaitableTimer(nint timerHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForMultipleObjects(uint count, nint* handles, int waitAll, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial int CloseHandle(nint handle);
}
