using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Deskband11Lib.Native.Internal.Win32;

namespace Deskband11Lib.Native.Internal;

// Runs work on the thread that created it through a message-only window, so the existing message loop of that thread drives dispatch and timers.
internal sealed unsafe class MessageWindowDispatcher : IDisposable
{
    private const string WindowClassName = "Deskband11Lib.Native.MessageWindow";
    private const uint DispatchQueueMessage = NativeWin32Methods.ApplicationMessageBase + 1;
    private const uint TimerTickMessage = NativeWin32Methods.ApplicationMessageBase + 2;

    private static readonly ConcurrentDictionary<nint, MessageWindowDispatcher> s_dispatchers = new();
    private static readonly Lock s_windowClassRegistrationLock = new();
    private static nint s_moduleHandle;

    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private readonly Dictionary<nuint, Action> _timerTicks = [];
    private readonly MessageWindowSynchronizationContext _synchronizationContext;
    private readonly HighResolutionTimerScheduler _timerScheduler;
    private readonly uint _ownerThreadIdentifier;
    private readonly nint _windowHandle;
    private nuint _lastTimerIdentifier;
    private int _isDispatchPosted;
    private int _processingDepth;
    private volatile bool _isDisposeRequested;
    private bool _isDisposed;

    public MessageWindowDispatcher()
    {
        _ownerThreadIdentifier = NativeWin32Methods.GetCurrentThreadId();
        _synchronizationContext = new MessageWindowSynchronizationContext(this);
        _windowHandle = CreateMessageWindow();
        _timerScheduler = new HighResolutionTimerScheduler(_windowHandle, TimerTickMessage);
        s_dispatchers[_windowHandle] = this;
    }

    public event Action<Exception>? UnhandledException;

    public SynchronizationContext SynchronizationContext => _synchronizationContext;

    public bool CheckAccess() => NativeWin32Methods.GetCurrentThreadId() == _ownerThreadIdentifier;

    public void Post(Action action)
    {
        if (_isDisposeRequested) return;

        _pendingActions.Enqueue(action);
        if (Interlocked.Exchange(ref _isDispatchPosted, 1) == 1) return;
        if (NativeWin32Methods.PostMessage(_windowHandle, DispatchQueueMessage, 0, 0) == 0) Volatile.Write(ref _isDispatchPosted, 0);
    }

    public nuint RegisterTimer(Action tick)
    {
        var timerIdentifier = ++_lastTimerIdentifier;
        _timerTicks[timerIdentifier] = tick;
        return timerIdentifier;
    }

    public void UnregisterTimer(nuint timerIdentifier)
    {
        _timerScheduler.Remove(timerIdentifier);
        _timerTicks.Remove(timerIdentifier);
    }

    public void StartTimer(nuint timerIdentifier, TimeSpan interval) => _timerScheduler.Start(timerIdentifier, interval);

    public void StopTimer(nuint timerIdentifier) => _timerScheduler.Stop(timerIdentifier);

    // Disposing while a message is being processed (eg: from an event callback) is deferred until that message returns.
    public void Dispose()
    {
        _isDisposeRequested = true;
        if (_processingDepth == 0) DestroyMessageWindow();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        if ((message is DispatchQueueMessage or TimerTickMessage) && s_dispatchers.TryGetValue(windowHandle, out var dispatcher))
        {
            // Exceptions must not cross the unmanaged boundary, so a failure that escapes the dispatcher is dropped here.
            try { dispatcher.ProcessMessage(message, (nuint)wParam); }
            catch (Exception) { }
            return 0;
        }

        return NativeWin32Methods.DefWindowProc(windowHandle, message, wParam, lParam);
    }

    private void ProcessMessage(uint message, nuint timerIdentifier)
    {
        var previousSynchronizationContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
        _processingDepth++;
        try
        {
            if (message == DispatchQueueMessage) ProcessPendingActions();
            else if (_timerScheduler.CompleteTick(timerIdentifier) && _timerTicks.TryGetValue(timerIdentifier, out var tick)) Invoke(tick);
        }
        finally
        {
            _processingDepth--;
            SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
            if (_processingDepth == 0 && _isDisposeRequested) DestroyMessageWindow();
        }
    }

    // Actions queued while this batch runs are handled by the next posted message, so other messages keep flowing.
    private void ProcessPendingActions()
    {
        Volatile.Write(ref _isDispatchPosted, 0);

        var remainingActionCount = _pendingActions.Count;
        while (remainingActionCount-- > 0 && !_isDisposeRequested && _pendingActions.TryDequeue(out var action)) Invoke(action);
    }

    private void Invoke(Action action)
    {
        try { action(); }
        catch (Exception exception) { UnhandledException?.Invoke(exception); }
    }

    private void DestroyMessageWindow()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _timerScheduler.Dispose();
        _ = s_dispatchers.TryRemove(_windowHandle, out _);
        _pendingActions.Clear();
        _timerTicks.Clear();
        _ = NativeWin32Methods.DestroyWindow(_windowHandle);
    }

    private static nint CreateMessageWindow()
    {
        var moduleHandle = EnsureWindowClassRegistered();
        fixed (char* className = WindowClassName)
        {
            var windowHandle = NativeWin32Methods.CreateWindowEx(0, className, null, 0, 0, 0, 0, 0, NativeWin32Methods.MessageOnlyParentWindow, 0, moduleHandle, null);
            if (windowHandle == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            return windowHandle;
        }
    }

    private static nint EnsureWindowClassRegistered()
    {
        lock (s_windowClassRegistrationLock)
        {
            if (s_moduleHandle != 0) return s_moduleHandle;

            var moduleHandle = GetCurrentModuleHandle();
            fixed (char* className = WindowClassName)
            {
                var windowClass = new WindowClassEx { Size = (uint)sizeof(WindowClassEx), WindowProcedure = &WindowProcedure, InstanceHandle = moduleHandle, ClassName = className };
                if (NativeWin32Methods.RegisterClassEx(&windowClass) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            s_moduleHandle = moduleHandle;
            return moduleHandle;
        }
    }

    // The window class is registered against this library instead of the host executable, so separate copies of the library loaded into one process keep separate classes.
    private static nint GetCurrentModuleHandle()
    {
        var moduleHandle = nint.Zero;
        if (NativeWin32Methods.GetModuleHandleEx(NativeWin32Methods.ModuleHandleFromAddressFlag | NativeWin32Methods.ModuleHandleUnchangedReferenceCountFlag, (void*)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WindowProcedure, &moduleHandle) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return moduleHandle;
    }
}
