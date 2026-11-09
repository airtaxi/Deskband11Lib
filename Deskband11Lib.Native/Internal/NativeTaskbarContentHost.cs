using System.Runtime.Versioning;
using Deskband11Lib.Core;
using Deskband11Lib.Native.Internal.Interop;

namespace Deskband11Lib.Native.Internal;

[SupportedOSPlatform("windows10.0.22000.0")]
internal sealed class NativeTaskbarContentHost : TaskbarContentHostBase
{
    private const int MaximumPendingEventCount = 256;

    private readonly MessageWindowDispatcher _dispatcher;
    private readonly NativeTaskbarHostPlatformAdapter _platformAdapter;
    private readonly unsafe delegate* unmanaged[Cdecl]<nint, NativeHostEvent*, void> _eventCallback;
    private readonly nint _userData;
    private readonly LinkedList<PendingNativeHostEvent> _pendingEvents = new();
    private bool _isEventFlushPosted;
    private bool _isHostDisposed;

    private unsafe NativeTaskbarContentHost(MessageWindowDispatcher dispatcher, NativeTaskbarHostPlatformAdapter platformAdapter, TaskbarContentHostOptions options, delegate* unmanaged[Cdecl]<nint, NativeHostEvent*, void> eventCallback, nint userData) : base(platformAdapter, options)
    {
        _dispatcher = dispatcher;
        _platformAdapter = platformAdapter;
        _eventCallback = eventCallback;
        _userData = userData;
        _dispatcher.UnhandledException += OnDispatcherUnhandledException;
        _platformAdapter.ContentBoundsApplied += OnPlatformAdapterContentBoundsApplied;
        TaskbarWindowRecreated += OnTaskbarWindowRecreated;
        TaskbarWindowDisappeared += OnTaskbarWindowDisappeared;
        TaskbarDpiChanged += OnTaskbarDpiChanged;
        TaskbarWindowRecreationRequired += OnTaskbarWindowRecreationRequired;
        TaskbarEdgeChanged += OnTaskbarEdgeChanged;
    }

    public static unsafe NativeTaskbarContentHost Create(nint windowHandle, TaskbarContentHostOptions options, delegate* unmanaged[Cdecl]<nint, NativeHostEvent*, void> eventCallback, nint userData)
    {
        ValidateWindow(windowHandle);

        var dispatcher = new MessageWindowDispatcher();
        try { return new NativeTaskbarContentHost(dispatcher, new NativeTaskbarHostPlatformAdapter(windowHandle, options, dispatcher), options, eventCallback, userData); }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void BeginAttachWhenLayoutReady() => _ = AttachWhenLayoutReadyAndPublishAsync();

    public bool TryDequeueEvent(out NativeHostEvent nativeEvent)
    {
        if (_pendingEvents.First is not { } firstNode)
        {
            nativeEvent = default;
            return false;
        }

        _pendingEvents.RemoveFirst();
        if (firstNode.Value.ErrorMessage is { } errorMessage) NativeErrorState.SetLastErrorMessage(errorMessage);
        nativeEvent = firstNode.Value.Event;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isHostDisposed = true;
            _pendingEvents.Clear();
            _dispatcher.UnhandledException -= OnDispatcherUnhandledException;
            _platformAdapter.ContentBoundsApplied -= OnPlatformAdapterContentBoundsApplied;
        }

        base.Dispose(disposing);
        if (disposing) _dispatcher.Dispose();
    }

    private async Task AttachWhenLayoutReadyAndPublishAsync()
    {
        // Core resumes through the captured context after waiting for the taskbar, which brings the attach back to the host thread.
        SynchronizationContext.SetSynchronizationContext(_dispatcher.SynchronizationContext);
        try
        {
            await AttachWhenLayoutReadyAsync();
            PublishEvent(NativeHostEventKind.Attached);
        }
        catch (Exception exception) { PublishErrorEvent(NativeHostEventKind.AttachFailed, exception); }
    }

    private void PublishEvent(NativeHostEventKind kind) => EnqueueEvent(new PendingNativeHostEvent(CreateEvent(kind, NativeResult.Ok, 0, 0, 0), null));

    private void PublishErrorEvent(NativeHostEventKind kind, Exception exception) => EnqueueEvent(new PendingNativeHostEvent(CreateEvent(kind, NativeErrorState.GetResult(exception), 0, 0, 0), exception.Message));

    private NativeHostEvent CreateEvent(NativeHostEventKind kind, NativeResult result, double maxWidth, double width, double height) => new()
    {
        Kind = kind,
        Result = result,
        TaskbarEdge = TaskbarEdge,
        MaxWidth = maxWidth,
        Width = width,
        Height = height
    };

    // Events are delivered from a later message instead of from inside Core, so a callback can destroy the host without re-entering a layout pass.
    private unsafe void EnqueueEvent(PendingNativeHostEvent pendingEvent)
    {
        if (_isHostDisposed) return;

        // Animation frames apply bounds continuously, so only the latest pending bounds are kept.
        if (pendingEvent.Event.Kind == NativeHostEventKind.ContentBoundsChanged && _pendingEvents.Last is { Value.Event.Kind: NativeHostEventKind.ContentBoundsChanged } lastNode) lastNode.Value = pendingEvent;
        else
        {
            if (_pendingEvents.Count >= MaximumPendingEventCount) _pendingEvents.RemoveFirst();
            _pendingEvents.AddLast(pendingEvent);
        }

        if (_eventCallback == null || _isEventFlushPosted) return;

        _isEventFlushPosted = true;
        _dispatcher.Post(FlushPendingEvents);
    }

    private void FlushPendingEvents()
    {
        _isEventFlushPosted = false;
        while (!_isHostDisposed && _pendingEvents.First is { } firstNode)
        {
            _pendingEvents.RemoveFirst();
            InvokeEventCallback(firstNode.Value);
        }
    }

    private unsafe void InvokeEventCallback(PendingNativeHostEvent pendingEvent)
    {
        if (pendingEvent.ErrorMessage is { } errorMessage) NativeErrorState.SetLastErrorMessage(errorMessage);

        var nativeEvent = pendingEvent.Event;
        _eventCallback(_userData, &nativeEvent);
    }

    private static void ValidateWindow(nint windowHandle)
    {
        if (!TaskbarWindowHelper.IsWindowAlive(windowHandle)) throw new ArgumentException("The window handle does not refer to an existing window.");

        // Core mixes physical taskbar coordinates with the DPI of the hosted window, which only lines up for per-monitor DPI aware windows.
        var dpiAwareness = NativeWin32Methods.GetAwarenessFromDpiAwarenessContext(NativeWin32Methods.GetWindowDpiAwarenessContext(windowHandle));
        if (dpiAwareness != NativeWin32Methods.PerMonitorDpiAwareness) throw new NativeResultException(NativeResult.UnsupportedDpiAwareness, "The window must be per-monitor DPI aware.");
    }

    private void OnDispatcherUnhandledException(Exception exception) => PublishErrorEvent(NativeHostEventKind.Error, exception);

    private void OnPlatformAdapterContentBoundsApplied(double maxWidth, double width, double height) => EnqueueEvent(new PendingNativeHostEvent(CreateEvent(NativeHostEventKind.ContentBoundsChanged, NativeResult.Ok, maxWidth, width, height), null));

    private void OnTaskbarWindowRecreated(object? _, EventArgs __) => PublishEvent(NativeHostEventKind.TaskbarWindowRecreated);

    private void OnTaskbarWindowDisappeared(object? _, EventArgs __) => PublishEvent(NativeHostEventKind.TaskbarWindowDisappeared);

    private void OnTaskbarDpiChanged(object? _, EventArgs __) => PublishEvent(NativeHostEventKind.TaskbarDpiChanged);

    private void OnTaskbarWindowRecreationRequired(object? _, EventArgs __) => PublishEvent(NativeHostEventKind.TaskbarWindowRecreationRequired);

    private void OnTaskbarEdgeChanged(object? _, EventArgs __) => PublishEvent(NativeHostEventKind.TaskbarEdgeChanged);
}
