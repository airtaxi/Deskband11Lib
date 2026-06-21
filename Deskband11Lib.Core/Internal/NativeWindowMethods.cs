using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskband11Lib.Core.Internal;

internal static partial class NativeWindowMethods
{
    public static WINDOW_STYLE GetWindowStyle(HWND windowHandle)
    {
        var windowHandleValue = GetWindowHandleValue(windowHandle);
        var style = Environment.Is64BitProcess ? GetWindowLongPtr64(windowHandleValue, (int)WINDOW_LONG_PTR_INDEX.GWL_STYLE) : GetWindowLong32(windowHandleValue, (int)WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        return (WINDOW_STYLE)(int)style;
    }

    public static void SetWindowStyle(HWND windowHandle, WINDOW_STYLE windowStyle)
    {
        var windowHandleValue = GetWindowHandleValue(windowHandle);
        if (Environment.Is64BitProcess) _ = SetWindowLongPtr64(windowHandleValue, (int)WINDOW_LONG_PTR_INDEX.GWL_STYLE, (nint)(int)windowStyle);
        else _ = SetWindowLong32(windowHandleValue, (int)WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)windowStyle);
    }

    private static unsafe nint GetWindowHandleValue(HWND windowHandle) => (nint)windowHandle.Value;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial int GetWindowLong32(nint windowHandle, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial int SetWindowLong32(nint windowHandle, int index, int value);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr64(nint windowHandle, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr64(nint windowHandle, int index, nint value);
}
