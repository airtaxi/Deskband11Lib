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

    private readonly object _refreshLock = new();
    private int _cachedRightEdge;
    private bool _hasCachedRightEdge;
    private bool _isRefreshRunning;
    private bool _isDisposed;

    public bool TryGetTaskbarButtonsRightEdge(HWND taskbarWindow, RECT searchRectangle, out int rightEdge)
    {
        rightEdge = 0;
        if (taskbarWindow.IsNull) return false;

        QueueRefresh(taskbarWindow, searchRectangle);

        lock (_refreshLock)
        {
            if (!_hasCachedRightEdge) return false;

            rightEdge = _cachedRightEdge;
            return rightEdge > 0;
        }
    }

    public Task RefreshAsync(HWND taskbarWindow, RECT searchRectangle)
    {
        if (!TryBeginRefresh()) return Task.CompletedTask;

        return Task.Run(() => RefreshCachedRightEdge(taskbarWindow, searchRectangle));
    }

    public void Dispose()
    {
        lock (_refreshLock)
        {
            _isDisposed = true;
            _hasCachedRightEdge = false;
            _cachedRightEdge = 0;
        }
    }

    private void QueueRefresh(HWND taskbarWindow, RECT searchRectangle)
    {
        if (!TryBeginRefresh()) return;

        ThreadPool.QueueUserWorkItem(_ => RefreshCachedRightEdge(taskbarWindow, searchRectangle));
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

    private void RefreshCachedRightEdge(HWND taskbarWindow, RECT searchRectangle)
    {
        try
        {
            if (TryReadTaskbarButtonsRightEdge(taskbarWindow, searchRectangle, out var rightEdge))
            {
                lock (_refreshLock)
                {
                    if (!_isDisposed)
                    {
                        _cachedRightEdge = rightEdge;
                        _hasCachedRightEdge = true;
                    }
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
    private static unsafe bool TryReadTaskbarButtonsRightEdge(HWND taskbarWindow, RECT searchRectangle, out int rightEdge)
    {
        rightEdge = 0;
        if (!TryGetWindowProcessIdentifier(taskbarWindow, out var taskbarProcessIdentifier)) return false;

        IGeneratedUIAutomation? automation = null;
        IGeneratedUIAutomationCondition? trueCondition = null;
        IGeneratedUIAutomationElement? taskbarElement = null;
        IGeneratedUIAutomationElementArray? taskButtonElements = null;
        List<GeneratedRectangle> taskbarButtonRectangles = [];
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
                    if (!IsInsideSearchRectangle(searchRectangle, boundingRectangle)) continue;

                    taskbarButtonRectangles.Add(boundingRectangle);
                }
                finally { ReleaseComObject(taskButtonElement); }
            }

            rightEdge = GetContiguousTaskbarButtonsRightEdge(searchRectangle, taskbarButtonRectangles);
            return rightEdge > 0;
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

    private static int GetContiguousTaskbarButtonsRightEdge(RECT searchRectangle, List<GeneratedRectangle> taskbarButtonRectangles)
    {
        var rightEdge = searchRectangle.left;

        foreach (var rectangle in taskbarButtonRectangles.OrderBy(rectangle => rectangle.Left))
        {
            if (rectangle.Left > rightEdge + MaximumTaskbarButtonGap) break;

            rightEdge = Math.Max(rightEdge, rectangle.Right);
        }

        return rightEdge > searchRectangle.left ? rightEdge : 0;
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
