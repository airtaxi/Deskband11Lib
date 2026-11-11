using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Deskband11Lib.Core.Internal.GeneratedCom;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskband11Lib.Core.Internal;

internal sealed partial class TaskbarButtonReader : IDisposable
{
    private const uint ClassContextInProcServer = 0x1;
    private const uint CoInitializeMultithreaded = 0x0;
    private const int RpcChangedMode = unchecked((int)0x80010106);
    private const int ButtonControlTypeIdentifier = 50000;
    private const int MaximumTaskbarButtonGap = 16;
    private const int MaximumAbandonedRefreshCount = 4;
    private const string StartButtonAutomationIdentifier = "StartButton";
    private const string WidgetsButtonAutomationIdentifier = "WidgetsButton";
    private const string SystemTrayIconAutomationIdentifier = "SystemTrayIcon";
    private const string NotifyItemIconAutomationIdentifier = "NotifyItemIcon";

    // By default UI Automation waits up to 20 seconds for a provider in another process, so a refresh that runs longer than this is not expected to return on its own.
    private static readonly TimeSpan s_stalledRefreshTimeout = TimeSpan.FromSeconds(30);

    private readonly object _refreshLock = new();
    private TaskbarButtonGeometry _cachedGeometry;
    private bool _hasCachedGeometry;
    private bool _cachedIsVertical;
    private bool _isRefreshRunning;
    private long _refreshStartTimestamp;
    private long _currentRefreshIdentifier;
    private int _abandonedRefreshCount;
    private bool _isDisposed;

    public bool TryGetTaskbarButtonGeometry(HWND taskbarWindow, RECT searchRectangle, bool isVertical, out TaskbarButtonGeometry geometry)
    {
        geometry = default;
        if (taskbarWindow.IsNull) return false;

        QueueRefresh(taskbarWindow, searchRectangle, isVertical);

        lock (_refreshLock)
        {
            // A measurement taken before the taskbar changed orientation lies on the other axis, so it is ignored until a new one completes.
            if (!_hasCachedGeometry || _cachedIsVertical != isVertical) return false;
            geometry = _cachedGeometry;
            return geometry.StartButton.IsValid || geometry.WidgetsButton.IsValid || geometry.TaskbarButtonsGroup.IsValid || geometry.NotificationArea.IsValid;
        }
    }

    public bool TryGetCachedTaskbarButtonGeometry(out TaskbarButtonGeometry geometry)
    {
        lock (_refreshLock)
        {
            geometry = _cachedGeometry;
            return _hasCachedGeometry;
        }
    }

    public Task RefreshAsync(HWND taskbarWindow, RECT searchRectangle, bool isVertical)
    {
        if (!TryBeginRefresh(out var refreshIdentifier)) return Task.CompletedTask;

        return Task.Run(() => RefreshCachedGeometry(taskbarWindow, searchRectangle, isVertical, refreshIdentifier));
    }

    public void Dispose()
    {
        lock (_refreshLock)
        {
            _isDisposed = true;
            _hasCachedGeometry = false;
            _cachedGeometry = default;
        }
    }

    private void QueueRefresh(HWND taskbarWindow, RECT searchRectangle, bool isVertical)
    {
        if (!TryBeginRefresh(out var refreshIdentifier)) return;

        ThreadPool.QueueUserWorkItem(_ => RefreshCachedGeometry(taskbarWindow, searchRectangle, isVertical, refreshIdentifier));
    }

    private bool TryBeginRefresh(out long refreshIdentifier)
    {
        refreshIdentifier = 0;

        lock (_refreshLock)
        {
            if (_isDisposed) return false;
            if (_isRefreshRunning)
            {
                // A refresh stays blocked inside UI Automation for as long as a window it reads does not answer, so a stalled refresh is given up on and replaced.
                // The refreshes given up on are capped, so that blocked threads do not pile up while the cause persists.
                if (Stopwatch.GetElapsedTime(_refreshStartTimestamp) < s_stalledRefreshTimeout || _abandonedRefreshCount >= MaximumAbandonedRefreshCount) return false;
                _abandonedRefreshCount++;
            }

            _isRefreshRunning = true;
            _refreshStartTimestamp = Stopwatch.GetTimestamp();
            refreshIdentifier = ++_currentRefreshIdentifier;
            return true;
        }
    }

    private void RefreshCachedGeometry(HWND taskbarWindow, RECT searchRectangle, bool isVertical, long refreshIdentifier)
    {
        try
        {
            if (TryReadTaskbarGeometry(taskbarWindow, searchRectangle, isVertical, out var geometry))
            {
                lock (_refreshLock)
                {
                    // A refresh that was given up on returns late, with a measurement older than the one of the refresh that replaced it.
                    if (_isDisposed || refreshIdentifier != _currentRefreshIdentifier) return;

                    _cachedGeometry = geometry;
                    _cachedIsVertical = isVertical;
                    _hasCachedGeometry = true;
                }
            }
        }
        catch (Exception) { }
        finally
        {
            lock (_refreshLock)
            {
                if (refreshIdentifier == _currentRefreshIdentifier) _isRefreshRunning = false;
                else _abandonedRefreshCount--;
            }
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL2050", Justification = "UI Automation is an optional geometry probe. Failures keep the previous cached measurement.")]
    private static unsafe bool TryReadTaskbarGeometry(HWND taskbarWindow, RECT searchRectangle, bool isVertical, out TaskbarButtonGeometry geometry)
    {
        geometry = default;
        if (!TryGetWindowProcessIdentifier(taskbarWindow, out var taskbarProcessIdentifier)) return false;

        IGeneratedUIAutomation? automation = null;
        IGeneratedUIAutomationCondition? trueCondition = null;
        var spanCollector = new TaskbarElementSpanCollector(taskbarProcessIdentifier, searchRectangle, isVertical);
        var coInitializeResult = CoInitializeEx(0, CoInitializeMultithreaded);
        var shouldUninitializeCom = coInitializeResult is 0 or 1;
        if (coInitializeResult < 0 && coInitializeResult != RpcChangedMode) return false;

        try
        {
            var classIdentifier = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");
            var interfaceIdentifier = typeof(IGeneratedUIAutomation).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(&classIdentifier, 0, ClassContextInProcServer, &interfaceIdentifier, out automation));

            trueCondition = automation.CreateTrueCondition();
            foreach (var taskbarOwnedWindow in CollectTaskbarOwnedChildWindows(taskbarWindow, taskbarProcessIdentifier)) ReadWindowElements(automation, trueCondition, taskbarOwnedWindow, spanCollector);

            geometry = spanCollector.CreateGeometry();
            return geometry.StartButton.IsValid || geometry.WidgetsButton.IsValid || geometry.TaskbarButtonsGroup.IsValid || geometry.NotificationArea.IsValid;
        }
        catch (COMException) { return false; }
        finally
        {
            ReleaseComObject(trueCondition);
            ReleaseComObject(automation);
            if (shouldUninitializeCom) CoUninitialize();
        }
    }

    // Explorer keeps every element the layout measures in child windows of its own. The other child windows are content hosted by other processes,
    // and reading those would make the measurement wait on the threads that own them, so only the windows of the taskbar process are read.
    // Hidden windows are left out, because UI Automation does not expose them as part of the taskbar either.
    // The windows are collected before any of them is read, because the order of the child windows can change while UI Automation runs.
    private static List<HWND> CollectTaskbarOwnedChildWindows(HWND taskbarWindow, int taskbarProcessIdentifier)
    {
        List<HWND> taskbarOwnedWindows = [];
        HashSet<HWND> visitedWindows = [];

        for (var childWindow = PInvoke.GetWindow(taskbarWindow, GET_WINDOW_CMD.GW_CHILD); !childWindow.IsNull && visitedWindows.Add(childWindow); childWindow = PInvoke.GetWindow(childWindow, GET_WINDOW_CMD.GW_HWNDNEXT))
        {
            if (!PInvoke.IsWindowVisible(childWindow)) continue;
            if (!TryGetWindowProcessIdentifier(childWindow, out var childProcessIdentifier) || childProcessIdentifier != taskbarProcessIdentifier) continue;
            taskbarOwnedWindows.Add(childWindow);
        }

        return taskbarOwnedWindows;
    }

    private static void ReadWindowElements(IGeneratedUIAutomation automation, IGeneratedUIAutomationCondition trueCondition, HWND window, TaskbarElementSpanCollector spanCollector)
    {
        IGeneratedUIAutomationElement? windowElement = null;
        IGeneratedUIAutomationElementArray? elements = null;

        try
        {
            windowElement = automation.ElementFromHandle(window);
            elements = windowElement.FindAll(GeneratedTreeScope.Subtree, trueCondition);
            var count = elements.GetLength();

            for (var index = 0; index < count; index++)
            {
                var element = elements.GetElement(index);
                try { spanCollector.Add(element); }
                finally { ReleaseComObject(element); }
            }
        }
        // Explorer can destroy a window while it is being read. Such a window has nothing left to measure, so the remaining windows are still read.
        catch (COMException) when (!PInvoke.IsWindow(window)) { }
        finally
        {
            ReleaseComObject(elements);
            ReleaseComObject(windowElement);
        }
    }

    private static string TryGetAutomationIdentifier(IGeneratedUIAutomationElement element)
    {
        try
        {
            if (element.GetCurrentAutomationIdentifier(out var automationIdentifierHandle) < 0 || automationIdentifierHandle == 0) return string.Empty;
            try { return Marshal.PtrToStringBSTR(automationIdentifierHandle) ?? string.Empty; }
            finally { Marshal.FreeBSTR(automationIdentifierHandle); }
        }
        catch { return string.Empty; }
    }

    private static AxisSpan ResolveTaskbarButtonsGroup(AxisSpan startButtonSpan, List<AxisSpan> taskbarAppButtonSpans)
    {
        if (taskbarAppButtonSpans.Count == 0) return AxisSpan.Invalid;

        var anchor = startButtonSpan.IsValid ? startButtonSpan.End : 0;
        var groupEnd = anchor;
        var groupStart = int.MaxValue;

        foreach (var buttonSpan in taskbarAppButtonSpans.OrderBy(buttonSpan => buttonSpan.Start))
        {
            if (buttonSpan.Start > groupEnd + MaximumTaskbarButtonGap) break;

            groupStart = Math.Min(groupStart, buttonSpan.Start);
            groupEnd = Math.Max(groupEnd, buttonSpan.End);
        }

        return groupEnd > anchor ? new AxisSpan(groupStart, groupEnd) : AxisSpan.Invalid;
    }

    private static bool IsVisibleTaskbarButton(IGeneratedUIAutomationElement element, int taskbarProcessIdentifier)
    {
        if (element.GetCurrentProcessIdentifier(out var processIdentifier) < 0 || processIdentifier != taskbarProcessIdentifier) return false;
        if (element.GetCurrentControlType(out var controlType) < 0 || controlType != ButtonControlTypeIdentifier) return false;
        if (element.GetCurrentIsOffscreen(out var isOffscreen) < 0 || isOffscreen != 0) return false;
        return true;
    }

    private static bool TryGetWindowProcessIdentifier(HWND windowHandle, out int processIdentifier)
    {
        processIdentifier = 0;
        var threadIdentifier = PInvoke.GetWindowThreadProcessId(windowHandle, out var nativeProcessIdentifier);
        if (threadIdentifier == 0 || nativeProcessIdentifier == 0 || nativeProcessIdentifier > int.MaxValue) return false;

        processIdentifier = (int)nativeProcessIdentifier;
        return true;
    }

    private static bool TryGetBoundingRectangle(IGeneratedUIAutomationElement element, out GeneratedRectangle boundingRectangle)
    {
        boundingRectangle = default;
        if (element.GetCurrentBoundingRectangle(out boundingRectangle) < 0) return false;
        return boundingRectangle.Right > boundingRectangle.Left && boundingRectangle.Bottom > boundingRectangle.Top;
    }

    private static bool IsInsideSearchRectangle(RECT rectangle, GeneratedRectangle generatedRectangle)
    {
        return generatedRectangle.Right > rectangle.left && generatedRectangle.Left < rectangle.right && generatedRectangle.Top < rectangle.bottom && generatedRectangle.Bottom > rectangle.top;
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject)) Marshal.ReleaseComObject(comObject);
    }

    [LibraryImport("ole32.dll")]
    private static unsafe partial int CoCreateInstance(Guid* classIdentifier, nint outerUnknown, uint classContext, Guid* interfaceIdentifier, out IGeneratedUIAutomation automation);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint coInitialize);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    // Sorts the elements of one measurement into the spans that make up the taskbar button geometry.
    private sealed class TaskbarElementSpanCollector(int taskbarProcessIdentifier, RECT searchRectangle, bool isVertical)
    {
        private readonly List<AxisSpan> _taskbarAppButtonSpans = [];
        private AxisSpan _startButtonSpan = AxisSpan.Invalid;
        private AxisSpan _widgetsButtonSpan = AxisSpan.Invalid;
        private int _notificationAreaStart = int.MaxValue;
        private int _notificationAreaEnd = int.MinValue;

        public void Add(IGeneratedUIAutomationElement element)
        {
            var automationIdentifier = TryGetAutomationIdentifier(element);
            var isNotificationAreaElement = automationIdentifier == SystemTrayIconAutomationIdentifier || automationIdentifier == NotifyItemIconAutomationIdentifier;

            if (!isNotificationAreaElement && !IsVisibleTaskbarButton(element, taskbarProcessIdentifier)) return;
            if (!TryGetBoundingRectangle(element, out var boundingRectangle)) return;
            if (!IsInsideSearchRectangle(searchRectangle, boundingRectangle)) return;

            var elementSpan = TaskbarAxisRectangle.FromRectangle(boundingRectangle, isVertical).MainSpan;
            if (isNotificationAreaElement)
            {
                _notificationAreaStart = Math.Min(_notificationAreaStart, elementSpan.Start);
                _notificationAreaEnd = Math.Max(_notificationAreaEnd, elementSpan.End);
            }
            else if (automationIdentifier == StartButtonAutomationIdentifier) _startButtonSpan = elementSpan;
            else if (automationIdentifier == WidgetsButtonAutomationIdentifier) _widgetsButtonSpan = elementSpan;
            else _taskbarAppButtonSpans.Add(elementSpan);
        }

        public TaskbarButtonGeometry CreateGeometry()
        {
            var taskbarButtonsGroup = ResolveTaskbarButtonsGroup(_startButtonSpan, _taskbarAppButtonSpans);
            var notificationAreaSpan = _notificationAreaStart <= _notificationAreaEnd ? new AxisSpan(_notificationAreaStart, _notificationAreaEnd) : AxisSpan.Invalid;
            return new TaskbarButtonGeometry(_startButtonSpan, _widgetsButtonSpan, taskbarButtonsGroup, notificationAreaSpan);
        }
    }
}
