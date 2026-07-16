using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed class TaskbarSlotRegistry : IDisposable
{
    private readonly SlotRegistryStore _store = new();

    private bool _isDisposed;
    private bool _hasRegistered;
    private nint _registeredWindowHandle;

    public unsafe TaskbarSlotInfo Register(HWND window, double preferredWidth, TaskbarContentPlacement actualPlacement, int monitorIdentity, ushort manualSlotPriority, bool allowFixedSlotResize, HWND taskbarWindow)
    {
        if (window.IsNull) throw new ArgumentException("Window handle is null.", nameof(window));
        if (preferredWidth < 0 && preferredWidth != double.MaxValue) throw new ArgumentOutOfRangeException(nameof(preferredWidth), "Preferred width must be non-negative or double.MaxValue for stretch.");

        var windowHandle = (nint)window.Value;
        var taskbarHandle = (nint)taskbarWindow.Value;
        var slotIndex = _store.RegisterOrUpdate(windowHandle, taskbarHandle, preferredWidth, actualPlacement, monitorIdentity, manualSlotPriority, allowFixedSlotResize);

        _registeredWindowHandle = windowHandle;
        _hasRegistered = true;

        return new TaskbarSlotInfo(slotIndex, manualSlotPriority, preferredWidth, actualPlacement, monitorIdentity, windowHandle, allowFixedSlotResize);
    }

    public unsafe void UpdateActualPlacement(HWND window, TaskbarContentPlacement actualPlacement)
    {
        if (window.IsNull) return;
        _store.UpdateActualPlacement((nint)window.Value, actualPlacement);
    }

    public unsafe void UpdateMonitorIdentity(HWND window, int monitorIdentity)
    {
        if (window.IsNull) return;
        _store.UpdateMonitorIdentity((nint)window.Value, monitorIdentity);
    }

    public unsafe List<TaskbarSlotInfo> CollectSlots(HWND taskbarWindow, HWND? excludeWindow = null)
    {
        if (taskbarWindow.IsNull) return [];
        var taskbarHandle = (nint)taskbarWindow.Value;
        var excludeHandle = excludeWindow.HasValue ? (nint?)excludeWindow.Value.Value : null;
        return _store.CollectSlots(taskbarHandle, excludeHandle);
    }

    public void Unregister()
    {
        if (!_hasRegistered || _registeredWindowHandle == 0) return;

        _store.Unregister(_registeredWindowHandle);
        _hasRegistered = false;
        _registeredWindowHandle = 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Unregister();
        _store.Dispose();
        _isDisposed = true;
    }
}