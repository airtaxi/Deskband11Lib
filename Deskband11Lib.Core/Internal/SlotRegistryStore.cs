using System.IO.MemoryMappedFiles;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed class SlotRegistryStore : IDisposable
{
    private const string MemoryMappedFileName = "Local\\Deskband11Lib.SlotRegistry";
    private const string SlotMutexName = "Local\\Deskband11Lib.SlotRegistry.Mutex";
    private const int MaximumSlotCount = 64;
    private const int SlotEntrySize = 56;

    private static readonly int s_totalSize = MaximumSlotCount * SlotEntrySize;

    private static readonly int s_slotIndexOffset = 0;
    private static readonly int s_manualSlotPriorityOffset = 2;
    private static readonly int s_monitorIdentityOffset = 4;
    private static readonly int s_actualPlacementOffset = 8;
    private static readonly int s_windowHandleOffset = 16;
    private static readonly int s_taskbarHandleOffset = 24;
    private static readonly int s_preferredWidthBitsOffset = 32;
    private static readonly int s_isActiveOffset = 40;
    private static readonly int s_allowFixedSlotResizeOffset = 41;
    private static readonly int s_lastHeartbeatOffset = 48;

    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _viewAccessor;
    private readonly Mutex _mutex;
    private bool _isDisposed;

    public SlotRegistryStore()
    {
        _memoryMappedFile = MemoryMappedFile.CreateOrOpen(MemoryMappedFileName, s_totalSize, MemoryMappedFileAccess.ReadWrite);
        _viewAccessor = _memoryMappedFile.CreateViewAccessor(0, s_totalSize, MemoryMappedFileAccess.ReadWrite);
        _mutex = new Mutex(false, SlotMutexName);
    }

    public ushort RegisterOrUpdate(nint windowHandle, nint taskbarHandle, double preferredWidth, TaskbarContentPlacement actualPlacement, int monitorIdentity, ushort manualSlotPriority, bool allowFixedSlotResize)
    {
        if (!_mutex.WaitOne(5000)) throw new InvalidOperationException("Failed to acquire slot registry mutex.");
        try
        {
            EvictStaleSlots();

            var existingIndex = FindSlotByWindowHandle(windowHandle);
            if (existingIndex >= 0)
            {
                WriteSlot(existingIndex, windowHandle, taskbarHandle, preferredWidth, actualPlacement, monitorIdentity, manualSlotPriority, (ushort)existingIndex, allowFixedSlotResize);
                return (ushort)existingIndex;
            }

            var slotIndex = ClaimNextSlotIndex();
            var entryIndex = FindFreeEntry();
            if (entryIndex < 0) throw new InvalidOperationException("Slot registry is full.");
            WriteSlot(entryIndex, windowHandle, taskbarHandle, preferredWidth, actualPlacement, monitorIdentity, manualSlotPriority, slotIndex, allowFixedSlotResize);
            return slotIndex;
        }
        finally { _mutex.ReleaseMutex(); }
    }

    public void UpdateActualPlacement(nint windowHandle, TaskbarContentPlacement actualPlacement)
    {
        if (!_mutex.WaitOne(5000)) return;
        try
        {
            for (var index = 0; index < MaximumSlotCount; index++)
            {
                var baseOffset = index * SlotEntrySize;
                if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;
                if (ReadNInt(baseOffset + s_windowHandleOffset) != windowHandle) continue;

                _viewAccessor.Write(baseOffset + s_actualPlacementOffset, (int)actualPlacement);
                return;
            }
        }
        finally { _mutex.ReleaseMutex(); }
    }

    public void UpdateMonitorIdentity(nint windowHandle, int monitorIdentity)
    {
        if (!_mutex.WaitOne(5000)) return;
        try
        {
            for (var index = 0; index < MaximumSlotCount; index++)
            {
                var baseOffset = index * SlotEntrySize;
                if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;
                if (ReadNInt(baseOffset + s_windowHandleOffset) != windowHandle) continue;

                _viewAccessor.Write(baseOffset + s_monitorIdentityOffset, monitorIdentity);
                return;
            }
        }
        finally { _mutex.ReleaseMutex(); }
    }

    public void Unregister(nint windowHandle)
    {
        if (!_mutex.WaitOne(5000)) return;
        try
        {
            for (var index = 0; index < MaximumSlotCount; index++)
            {
                var baseOffset = index * SlotEntrySize;
                if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;
                if (ReadNInt(baseOffset + s_windowHandleOffset) != windowHandle) continue;

                _viewAccessor.Write(baseOffset + s_isActiveOffset, false);
                return;
            }
        }
        finally { _mutex.ReleaseMutex(); }
    }

    public List<TaskbarSlotInfo> CollectSlots(nint taskbarHandle, nint? excludeWindowHandle)
    {
        if (!_mutex.WaitOne(5000)) return [];
        try
        {
            EvictStaleSlots();

            var slots = new List<TaskbarSlotInfo>();
            for (var index = 0; index < MaximumSlotCount; index++)
            {
                var baseOffset = index * SlotEntrySize;
                if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;

                var slotTaskbarHandle = ReadNInt(baseOffset + s_taskbarHandleOffset);
                if (slotTaskbarHandle != taskbarHandle) continue;

                var slotWindowHandle = ReadNInt(baseOffset + s_windowHandleOffset);
                if (excludeWindowHandle.HasValue && slotWindowHandle == excludeWindowHandle.Value) continue;

                var slotIndex = _viewAccessor.ReadUInt16(baseOffset + s_slotIndexOffset);
                var manualSlotPriority = _viewAccessor.ReadUInt16(baseOffset + s_manualSlotPriorityOffset);
                var monitorIdentity = _viewAccessor.ReadInt32(baseOffset + s_monitorIdentityOffset);
                var actualPlacement = (TaskbarContentPlacement)_viewAccessor.ReadInt32(baseOffset + s_actualPlacementOffset);
                var preferredWidthBits = _viewAccessor.ReadUInt64(baseOffset + s_preferredWidthBitsOffset);
                var preferredWidth = BitConverter.UInt64BitsToDouble(preferredWidthBits);
                var allowFixedSlotResize = _viewAccessor.ReadBoolean(baseOffset + s_allowFixedSlotResizeOffset);

                slots.Add(new TaskbarSlotInfo(slotIndex, manualSlotPriority, preferredWidth, actualPlacement, monitorIdentity, slotWindowHandle, allowFixedSlotResize));
            }

            return slots;
        }
        finally { _mutex.ReleaseMutex(); }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _viewAccessor.Dispose();
        _memoryMappedFile.Dispose();
        _mutex.Dispose();
        _isDisposed = true;
    }

    private ushort ClaimNextSlotIndex()
    {
        var maxSlotIndex = -1;
        for (var index = 0; index < MaximumSlotCount; index++)
        {
            var baseOffset = index * SlotEntrySize;
            if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;
            var slotIndex = _viewAccessor.ReadUInt16(baseOffset + s_slotIndexOffset);
            if (slotIndex > maxSlotIndex) maxSlotIndex = slotIndex;
        }

        return (ushort)((maxSlotIndex + 1) % 65536);
    }

    private int FindSlotByWindowHandle(nint windowHandle)
    {
        for (var index = 0; index < MaximumSlotCount; index++)
        {
            var baseOffset = index * SlotEntrySize;
            if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;
            if (ReadNInt(baseOffset + s_windowHandleOffset) == windowHandle) return index;
        }

        return -1;
    }

    private int FindFreeEntry()
    {
        for (var index = 0; index < MaximumSlotCount; index++)
        {
            var baseOffset = index * SlotEntrySize;
            if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) return index;
        }

        return -1;
    }

    private void WriteSlot(int entryIndex, nint windowHandle, nint taskbarHandle, double preferredWidth, TaskbarContentPlacement actualPlacement, int monitorIdentity, ushort manualSlotPriority, ushort slotIndex, bool allowFixedSlotResize)
    {
        var baseOffset = entryIndex * SlotEntrySize;
        _viewAccessor.Write(baseOffset + s_slotIndexOffset, slotIndex);
        _viewAccessor.Write(baseOffset + s_manualSlotPriorityOffset, manualSlotPriority);
        _viewAccessor.Write(baseOffset + s_monitorIdentityOffset, monitorIdentity);
        _viewAccessor.Write(baseOffset + s_actualPlacementOffset, (int)actualPlacement);
        WriteNInt(baseOffset + s_windowHandleOffset, windowHandle);
        WriteNInt(baseOffset + s_taskbarHandleOffset, taskbarHandle);
        _viewAccessor.Write(baseOffset + s_preferredWidthBitsOffset, BitConverter.DoubleToUInt64Bits(preferredWidth));
        _viewAccessor.Write(baseOffset + s_isActiveOffset, true);
        _viewAccessor.Write(baseOffset + s_allowFixedSlotResizeOffset, allowFixedSlotResize);
        _viewAccessor.Write(baseOffset + s_lastHeartbeatOffset, Environment.TickCount64);
    }

    private void EvictStaleSlots()
    {
        for (var index = 0; index < MaximumSlotCount; index++)
        {
            var baseOffset = index * SlotEntrySize;
            if (!_viewAccessor.ReadBoolean(baseOffset + s_isActiveOffset)) continue;

            var windowHandleValue = ReadNInt(baseOffset + s_windowHandleOffset);
            var windowHandle = new HWND((nint)windowHandleValue);
            if (!IsWindowAlive(windowHandle)) _viewAccessor.Write(baseOffset + s_isActiveOffset, false);
        }
    }

    private nint ReadNInt(int offset) => (nint)_viewAccessor.ReadInt64(offset);

    private void WriteNInt(int offset, nint value) => _viewAccessor.Write(offset, (long)value);

    private static bool IsWindowAlive(HWND windowHandle)
    {
        if (windowHandle.IsNull) return false;
        return PInvoke.IsWindow(windowHandle);
    }
}