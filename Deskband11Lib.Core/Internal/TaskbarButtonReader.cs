using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Deskband11Lib.Core.Internal.GeneratedCom;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed partial class TaskbarButtonReader : IDisposable
{
    private const uint ClassContextInProcServer = 0x1;
    private const uint CoInitializeMultithreaded = 0x0;
    private const int RpcChangedMode = unchecked((int)0x80010106);
    private const int ButtonControlTypeIdentifier = 50000;
    private const int MaximumTaskbarButtonGap = 16;
    private const string StartButtonAutomationIdentifier = "StartButton";
    private const string WidgetsButtonAutomationIdentifier = "WidgetsButton";
    private const string SystemTrayIconAutomationIdentifier = "SystemTrayIcon";
    private const string NotifyItemIconAutomationIdentifier = "NotifyItemIcon";

    private readonly object _refreshLock = new();
    private TaskbarButtonGeometry _cachedGeometry;
    private bool _hasCachedGeometry;
    private bool _isRefreshRunning;
    private bool _isDisposed;

    public bool TryGetTaskbarButtonGeometry(HWND taskbarWindow, RECT searchRectangle, out TaskbarButtonGeometry geometry)
    {
        geometry = default;
        if (taskbarWindow.IsNull) return false;

        QueueRefresh(taskbarWindow, searchRectangle);

        lock (_refreshLock)
        {
            if (!_hasCachedGeometry) return false;
            geometry = _cachedGeometry;
            return geometry.StartButton.IsValid || geometry.WidgetsButton.IsValid || geometry.TaskbarButtonsGroup.IsValid || geometry.NotificationArea.IsValid;
        }
    }

    public Task RefreshAsync(HWND taskbarWindow, RECT searchRectangle)
    {
        if (!TryBeginRefresh()) return Task.CompletedTask;

        return Task.Run(() => RefreshCachedGeometry(taskbarWindow, searchRectangle));
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

    private void QueueRefresh(HWND taskbarWindow, RECT searchRectangle)
    {
        if (!TryBeginRefresh()) return;

        ThreadPool.QueueUserWorkItem(_ => RefreshCachedGeometry(taskbarWindow, searchRectangle));
    }

    private bool TryBeginRefresh()
    {
        lock (_refreshLock)
        {
            if (_isDisposed || _isRefreshRunning) return false;

            _isRefreshRunning = true;
            return true;
        }
    }

    private void RefreshCachedGeometry(HWND taskbarWindow, RECT searchRectangle)
    {
        try
        {
            if (TryReadTaskbarGeometry(taskbarWindow, searchRectangle, out var geometry))
            {
                lock (_refreshLock)
                {
                    if (_isDisposed) return;

                    _cachedGeometry = geometry;
                    _hasCachedGeometry = true;
                }
            }
        }
        catch (Exception) { }
        finally
        {
            lock (_refreshLock)
            {
                _isRefreshRunning = false;
            }
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL2050", Justification = "UI Automation is an optional geometry probe. Failures keep the previous cached measurement.")]
    private static unsafe bool TryReadTaskbarGeometry(HWND taskbarWindow, RECT searchRectangle, out TaskbarButtonGeometry geometry)
    {
        geometry = default;
        if (!TryGetWindowProcessIdentifier(taskbarWindow, out var taskbarProcessIdentifier)) return false;

        IGeneratedUIAutomation? automation = null;
        IGeneratedUIAutomationCondition? trueCondition = null;
        IGeneratedUIAutomationElement? taskbarElement = null;
        IGeneratedUIAutomationElementArray? taskButtonElements = null;
        List<GeneratedRectangle> taskbarAppButtonRectangles = [];
        var startButtonSpan = ButtonSpan.Invalid;
        var widgetsButtonSpan = ButtonSpan.Invalid;
        var notificationAreaLeft = int.MaxValue;
        var notificationAreaRight = int.MinValue;
        var coInitializeResult = CoInitializeEx(0, CoInitializeMultithreaded);
        var shouldUninitializeCom = coInitializeResult is 0 or 1;
        if (coInitializeResult < 0 && coInitializeResult != RpcChangedMode) return false;

        try
        {
            var classIdentifier = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");
            var interfaceIdentifier = typeof(IGeneratedUIAutomation).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(&classIdentifier, 0, ClassContextInProcServer, &interfaceIdentifier, out automation));

            trueCondition = automation.CreateTrueCondition();
            taskbarElement = automation.ElementFromHandle(taskbarWindow);
            taskButtonElements = taskbarElement.FindAll(GeneratedTreeScope.Descendants, trueCondition);
            var count = taskButtonElements.GetLength();

            for (var index = 0; index < count; index++)
            {
                var taskButtonElement = taskButtonElements.GetElement(index);
                try
                {
                    if (!IsVisibleTaskbarButton(taskButtonElement, taskbarProcessIdentifier)) continue;
                    if (!TryGetBoundingRectangle(taskButtonElement, out var boundingRectangle)) continue;

                    var automationIdentifier = TryGetAutomationIdentifier(taskButtonElement);
                    if (automationIdentifier == SystemTrayIconAutomationIdentifier || automationIdentifier == NotifyItemIconAutomationIdentifier)
                    {
                        if (!IsInsideSearchRectangle(searchRectangle, boundingRectangle)) continue;
                        notificationAreaLeft = Math.Min(notificationAreaLeft, boundingRectangle.Left);
                        notificationAreaRight = Math.Max(notificationAreaRight, boundingRectangle.Right);
                        continue;
                    }

                    if (!IsInsideSearchRectangle(searchRectangle, boundingRectangle)) continue;

                    if (automationIdentifier == StartButtonAutomationIdentifier)
                    {
                        startButtonSpan = new ButtonSpan(boundingRectangle.Left, boundingRectangle.Right);
                        continue;
                    }
                    if (automationIdentifier == WidgetsButtonAutomationIdentifier)
                    {
                        widgetsButtonSpan = new ButtonSpan(boundingRectangle.Left, boundingRectangle.Right);
                        continue;
                    }

                    taskbarAppButtonRectangles.Add(boundingRectangle);
                }
                finally { ReleaseComObject(taskButtonElement); }
            }

            var taskbarButtonsGroup = ResolveTaskbarButtonsGroup(startButtonSpan, taskbarAppButtonRectangles);
            var notificationAreaSpan = notificationAreaLeft <= notificationAreaRight ? new ButtonSpan(notificationAreaLeft, notificationAreaRight) : ButtonSpan.Invalid;
            geometry = new TaskbarButtonGeometry(startButtonSpan, widgetsButtonSpan, taskbarButtonsGroup, notificationAreaSpan);
            return startButtonSpan.IsValid || widgetsButtonSpan.IsValid || taskbarButtonsGroup.IsValid || notificationAreaSpan.IsValid;
        }
        catch (COMException) { return false; }
        finally
        {
            ReleaseComObject(taskButtonElements);
            ReleaseComObject(taskbarElement);
            ReleaseComObject(trueCondition);
            ReleaseComObject(automation);
            if (shouldUninitializeCom) CoUninitialize();
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

    private static ButtonSpan ResolveTaskbarButtonsGroup(ButtonSpan startButtonSpan, List<GeneratedRectangle> taskbarAppButtonRectangles)
    {
        if (taskbarAppButtonRectangles.Count == 0) return ButtonSpan.Invalid;

        var anchor = startButtonSpan.IsValid ? startButtonSpan.Right : 0;
        var rightEdge = anchor;
        var leftEdge = int.MaxValue;

        foreach (var rectangle in taskbarAppButtonRectangles.OrderBy(rectangle => rectangle.Left))
        {
            if (rectangle.Left > rightEdge + MaximumTaskbarButtonGap) break;

            leftEdge = Math.Min(leftEdge, rectangle.Left);
            rightEdge = Math.Max(rightEdge, rectangle.Right);
        }

        return rightEdge > anchor ? new ButtonSpan(leftEdge, rightEdge) : ButtonSpan.Invalid;
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
}
