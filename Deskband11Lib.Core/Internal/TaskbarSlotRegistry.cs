using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskband11Lib.Core.Internal;

internal sealed class TaskbarSlotRegistry : IDisposable
{
    private const string SlotMarkerPropertyName = "Deskband11Lib.Slot";
    private const string SlotIndexPropertyName = "Deskband11Lib.Slot.Index";
    private const string PreferredWidthPropertyName = "Deskband11Lib.Slot.Width";
    private const string ActualPlacementPropertyName = "Deskband11Lib.Slot.Placement";
    private const string MonitorIdentityPropertyName = "Deskband11Lib.Slot.Monitor";
    private const string ManualSlotPriorityPropertyName = "Deskband11Lib.Slot.ManualPriority";
    private const string SlotClaimMutexName = "Global\\Deskband11Lib.SlotClaim";
    private const uint WaitForSingleObjectTimeoutMilliseconds = 5000;

    private static readonly unsafe delegate* unmanaged[Stdcall]<HWND, LPARAM, BOOL> s_enumChildWindowsCallback = &EnumChildWindowsCallback;

    private static readonly ushort s_slotMarkerAtom = PInvoke.GlobalAddAtom(SlotMarkerPropertyName);
    private static readonly ushort s_slotIndexAtom = PInvoke.GlobalAddAtom(SlotIndexPropertyName);
    private static readonly ushort s_preferredWidthAtom = PInvoke.GlobalAddAtom(PreferredWidthPropertyName);
    private static readonly ushort s_actualPlacementAtom = PInvoke.GlobalAddAtom(ActualPlacementPropertyName);
    private static readonly ushort s_monitorIdentityAtom = PInvoke.GlobalAddAtom(MonitorIdentityPropertyName);
    private static readonly ushort s_manualSlotPriorityAtom = PInvoke.GlobalAddAtom(ManualSlotPriorityPropertyName);

    static TaskbarSlotRegistry() => AppDomain.CurrentDomain.ProcessExit += static (_, _) => CleanupAtoms();

    private static unsafe void CleanupAtoms()
    {
        if (s_slotMarkerAtom != 0) PInvoke.GlobalDeleteAtom(s_slotMarkerAtom);
        if (s_slotIndexAtom != 0) PInvoke.GlobalDeleteAtom(s_slotIndexAtom);
        if (s_preferredWidthAtom != 0) PInvoke.GlobalDeleteAtom(s_preferredWidthAtom);
        if (s_actualPlacementAtom != 0) PInvoke.GlobalDeleteAtom(s_actualPlacementAtom);
        if (s_monitorIdentityAtom != 0) PInvoke.GlobalDeleteAtom(s_monitorIdentityAtom);
        if (s_manualSlotPriorityAtom != 0) PInvoke.GlobalDeleteAtom(s_manualSlotPriorityAtom);
    }

    private bool _isDisposed;
    private bool _hasRegistered;
    private HWND _registeredWindow;

    public static unsafe bool IsSlotWindow(HWND window)
    {
        if (window.IsNull) return false;
        var handle = PInvoke.GetProp(window, AtomToPCWSTR(s_slotMarkerAtom));
        return !handle.IsNull;
    }

    public unsafe TaskbarSlotInfo Register(HWND window, double preferredWidth, TaskbarContentPlacement actualPlacement, int monitorIdentity, ushort manualSlotPriority)
    {
        if (window.IsNull) throw new ArgumentException("Window handle is null.", nameof(window));
        if (preferredWidth < 0 && preferredWidth != double.MaxValue) throw new ArgumentOutOfRangeException(nameof(preferredWidth), "Preferred width must be non-negative or double.MaxValue for stretch.");

        ushort slotIndex;
        var mutexHandle = PInvoke.CreateMutex(null, false, SlotClaimMutexName);
        var shouldReleaseMutex = false;
        try
        {
            var waitResult = PInvoke.WaitForSingleObject(mutexHandle, WaitForSingleObjectTimeoutMilliseconds);
            if (waitResult != WAIT_EVENT.WAIT_OBJECT_0 && waitResult != WAIT_EVENT.WAIT_ABANDONED) throw new InvalidOperationException("Failed to acquire slot claim mutex.");
            shouldReleaseMutex = true;

            slotIndex = ClaimNextSlotIndex(window);
            WriteSlotProperties(window, slotIndex, preferredWidth, actualPlacement, monitorIdentity, manualSlotPriority);
        }
        finally
        {
            if (shouldReleaseMutex) PInvoke.ReleaseMutex(mutexHandle);
            mutexHandle.Dispose();
        }

        _registeredWindow = window;
        _hasRegistered = true;

        return new TaskbarSlotInfo(slotIndex, manualSlotPriority, preferredWidth, actualPlacement, monitorIdentity, (nint)window.Value);
    }

    public unsafe void UpdateActualPlacement(HWND window, TaskbarContentPlacement actualPlacement)
    {
        if (window.IsNull) return;
        PInvoke.SetProp(window, AtomToPCWSTR(s_actualPlacementAtom), EncodeIntToHandle((int)actualPlacement));
    }

    public unsafe void UpdateMonitorIdentity(HWND window, int monitorIdentity)
    {
        if (window.IsNull) return;
        PInvoke.SetProp(window, AtomToPCWSTR(s_monitorIdentityAtom), EncodeIntToHandle(monitorIdentity));
    }

    public static unsafe List<TaskbarSlotInfo> CollectSlots(HWND taskbarWindow, HWND? excludeWindow = null)
    {
        if (taskbarWindow.IsNull) return [];
        return EnumerateChildSlots(taskbarWindow, excludeWindow);
    }

    public unsafe void Unregister()
    {
        if (!_hasRegistered || _registeredWindow.IsNull) return;

        RemoveSlotProperties(_registeredWindow);
        _hasRegistered = false;
        _registeredWindow = default;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Unregister();
        _isDisposed = true;
    }

    private static unsafe ushort ClaimNextSlotIndex(HWND currentWindow)
    {
        var parent = PInvoke.GetParent(currentWindow);
        if (parent.IsNull) return 0;
        return ClaimNextSlotIndex(parent, currentWindow);
    }

    private static unsafe ushort ClaimNextSlotIndex(HWND parentWindow, HWND excludeWindow)
    {
        var existingSlots = EnumerateChildSlots(parentWindow, excludeWindow);
        if (existingSlots.Count == 0) return 0;
        return (ushort)((existingSlots.Max(slot => (long)slot.SlotIndex) + 1) % 65536);
    }

    private static unsafe List<TaskbarSlotInfo> EnumerateChildSlots(HWND parentWindow, HWND? excludeWindow)
    {
        var slots = new List<TaskbarSlotInfo>();
        var state = new EnumChildWindowsState(slots, excludeWindow);
        var handle = GCHandle.Alloc(state, GCHandleType.Normal);
        try { PInvoke.EnumChildWindows(parentWindow, s_enumChildWindowsCallback, new LPARAM(GCHandle.ToIntPtr(handle))); }
        finally { handle.Free(); }

        return slots;
    }

    private static unsafe void WriteSlotProperties(HWND window, ushort slotIndex, double preferredWidth, TaskbarContentPlacement actualPlacement, int monitorIdentity, ushort manualSlotPriority)
    {
        PInvoke.SetProp(window, AtomToPCWSTR(s_slotMarkerAtom), (HANDLE)(nint)1);
        PInvoke.SetProp(window, AtomToPCWSTR(s_slotIndexAtom), EncodeIntToHandle((int)slotIndex));
        PInvoke.SetProp(window, AtomToPCWSTR(s_preferredWidthAtom), (HANDLE)EncodeDoubleToIntPtr(preferredWidth));
        PInvoke.SetProp(window, AtomToPCWSTR(s_actualPlacementAtom), EncodeIntToHandle((int)actualPlacement));
        PInvoke.SetProp(window, AtomToPCWSTR(s_monitorIdentityAtom), EncodeIntToHandle(monitorIdentity));
        PInvoke.SetProp(window, AtomToPCWSTR(s_manualSlotPriorityAtom), EncodeIntToHandle(manualSlotPriority));
    }

    private static unsafe TaskbarSlotInfo? ReadSlotInfo(HWND window)
    {
        var slotIndexHandle = PInvoke.GetProp(window, AtomToPCWSTR(s_slotIndexAtom));
        if (slotIndexHandle.IsNull) return null;

        var preferredWidthHandle = PInvoke.GetProp(window, AtomToPCWSTR(s_preferredWidthAtom));
        var actualPlacementHandle = PInvoke.GetProp(window, AtomToPCWSTR(s_actualPlacementAtom));
        var monitorIdentityHandle = PInvoke.GetProp(window, AtomToPCWSTR(s_monitorIdentityAtom));
        var manualSlotPriorityHandle = PInvoke.GetProp(window, AtomToPCWSTR(s_manualSlotPriorityAtom));

        var slotIndex = (ushort)DecodeHandleToInt(slotIndexHandle);
        var preferredWidth = DecodeIntPtrToDouble((nint)preferredWidthHandle.Value);
        var actualPlacement = (TaskbarContentPlacement)DecodeHandleToInt(actualPlacementHandle);
        var monitorIdentity = DecodeHandleToInt(monitorIdentityHandle);
        var manualSlotPriority = (ushort)DecodeHandleToInt(manualSlotPriorityHandle);

        return new TaskbarSlotInfo(slotIndex, manualSlotPriority, preferredWidth, actualPlacement, monitorIdentity, (nint)window.Value);
    }

    private static unsafe void RemoveSlotProperties(HWND window)
    {
        PInvoke.RemoveProp(window, AtomToPCWSTR(s_slotMarkerAtom));
        PInvoke.RemoveProp(window, AtomToPCWSTR(s_slotIndexAtom));
        PInvoke.RemoveProp(window, AtomToPCWSTR(s_preferredWidthAtom));
        PInvoke.RemoveProp(window, AtomToPCWSTR(s_actualPlacementAtom));
        PInvoke.RemoveProp(window, AtomToPCWSTR(s_monitorIdentityAtom));
        PInvoke.RemoveProp(window, AtomToPCWSTR(s_manualSlotPriorityAtom));
    }

    private static nint EncodeDoubleToIntPtr(double value)
    {
        var bits = BitConverter.DoubleToUInt64Bits(value);
        return (nint)(bits & 0x7FFFFFFFFFFFFFFF);
    }

    private static double DecodeIntPtrToDouble(nint value)
    {
        var bits = (ulong)value;
        return BitConverter.UInt64BitsToDouble(bits);
    }

    private static unsafe HANDLE EncodeIntToHandle(int value) => new((nint)(value + 1));

    private static unsafe int DecodeHandleToInt(HANDLE handle) => (int)(nint)handle.Value - 1;

    private static unsafe PCWSTR AtomToPCWSTR(ushort atom) => new((char*)(nint)atom);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL EnumChildWindowsCallback(HWND childWindow, LPARAM lParam)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)(nint)lParam.Value);
        if (handle.Target is not EnumChildWindowsState state) return new BOOL(false);

        if (state.ExcludeWindow.HasValue && childWindow.Value == state.ExcludeWindow.Value.Value) return new BOOL(true);
        if (!IsSlotWindow(childWindow)) return new BOOL(true);

        var slotInfo = ReadSlotInfo(childWindow);
        if (slotInfo.HasValue) state.Slots.Add(slotInfo.Value);

        return new BOOL(true);
    }

    private sealed class EnumChildWindowsState(List<TaskbarSlotInfo> slots, HWND? excludeWindow)
    {
        public List<TaskbarSlotInfo> Slots { get; } = slots;

        public HWND? ExcludeWindow { get; } = excludeWindow;
    }
}
