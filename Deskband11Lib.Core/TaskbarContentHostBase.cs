using System.Diagnostics;
using System.Runtime.Versioning;
using Deskband11Lib.Core.Internal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskband11Lib.Core;

[SupportedOSPlatform("windows10.0.22000.0")]
public class TaskbarContentHostBase : IDisposable
{
    private static double? s_lastScaleFactor;
    private readonly ITaskbarHostPlatformAdapter _platformAdapter;
    private readonly TaskbarContentHostOptions _options;
    private readonly TaskbarWindowLocator _taskbarWindowLocator = new();
    private readonly TaskbarButtonReader _taskbarButtonReader = new();
    private readonly ExplorerRestartMonitorService _explorerRestartMonitorService = new();
    private readonly ITaskbarHostTimer _layoutRefreshTimer;
    private readonly ITaskbarHostTimer _layoutAnimationTimer;
    private readonly TaskbarLayoutCalculator _taskbarLayoutCalculator;
    private HWND _windowHandle;
    private HWND _originalParentWindow;
    private WINDOW_STYLE _originalWindowStyle;
    private TaskbarLayoutSnapshot _lastAppliedLayoutSnapshot;
    private TaskbarLayoutSnapshot _layoutAnimationStartSnapshot;
    private TaskbarLayoutSnapshot _layoutAnimationTargetSnapshot;
    private long _layoutAnimationStartTimestamp;
    private bool _hasAppliedLayoutSnapshot;
    private bool _isApplyingLayoutSnapshot;
    private bool _isDisposed;

    public TaskbarContentHostBase(ITaskbarHostPlatformAdapter platformAdapter, TaskbarContentHostOptions? options = null)
    {
        _platformAdapter = platformAdapter;
        _options = options ?? new TaskbarContentHostOptions();
        _taskbarLayoutCalculator = new TaskbarLayoutCalculator(_taskbarWindowLocator, _options, _taskbarButtonReader);
        _layoutRefreshTimer = _platformAdapter.CreateTimer(_options.LayoutRefreshInterval, OnLayoutRefreshTimerTick);
        _layoutAnimationTimer = _platformAdapter.CreateTimer(TimeSpan.FromMilliseconds(16), OnLayoutAnimationTimerTick);
        _explorerRestartMonitorService.TaskbarWindowRecreated += OnExplorerRestartMonitorServiceTaskbarWindowRecreated;
    }

    public event EventHandler? TaskbarWindowRecreated;

    public bool IsAttached { get; private set; }

    public TaskbarAlignment GetTaskbarAlignment() => TaskbarAlignmentDetector.ReadRegistryAlignment();

    public void Attach()
    {
        ThrowIfDisposed();
        if (IsAttached) return;

        AttachCore(false);
        RefreshLayout();
        _layoutRefreshTimer.Start();
    }

    public async Task AttachWhenLayoutReadyAsync()
    {
        ThrowIfDisposed();
        if (IsAttached) return;

        AttachCore(true);
        await RefreshTaskbarButtonMeasurementAsync();
        RefreshLayout();
        _layoutRefreshTimer.Start();
    }

    public void Detach()
    {
        if (!IsAttached) return;

        _layoutRefreshTimer.Stop();
        StopLayoutAnimation();
        _explorerRestartMonitorService.Stop();

        _ = PInvoke.SetWindowRgn(_windowHandle, HRGN.Null, true);
        NativeWindowMethods.SetWindowStyle(_windowHandle, _originalWindowStyle);
        PInvoke.SetParent(_windowHandle, _originalParentWindow);
        _platformAdapter.RestoreWindowAfterChildHosting();

        IsAttached = false;
        _hasAppliedLayoutSnapshot = false;
    }

    public void RefreshLayout()
    {
        ThrowIfDisposed();
        if (!IsAttached) return;

        ApplyHostedWindowStyle();

        var scaleFactor = GetScaleFactor();
        var snapshot = _taskbarLayoutCalculator.Calculate(_platformAdapter.RequestedWidth, _platformAdapter.RequestedHeight, scaleFactor);
        if (!snapshot.IsValid)
        {
            CollapseWindowRegion();
            return;
        }

        ApplyOrAnimateLayoutSnapshot(snapshot);
    }

    public void NotifyContentSizeChanged()
    {
        if (_isApplyingLayoutSnapshot) return;
        RefreshLayout();
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Dispose(true);
        GC.SuppressFinalize(this);
        _isDisposed = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        Detach();

        _layoutRefreshTimer.Dispose();
        _layoutAnimationTimer.Dispose();
        _explorerRestartMonitorService.TaskbarWindowRecreated -= OnExplorerRestartMonitorServiceTaskbarWindowRecreated;
        TaskbarWindowRecreated = null;

        _explorerRestartMonitorService.Dispose();
        _taskbarButtonReader.Dispose();
    }

    private void AttachCore(bool deferInitialLayout)
    {
        if (IsAttached) return;

        _platformAdapter.PrepareWindowForChildHosting();

        _windowHandle = new HWND(_platformAdapter.WindowHandle);
        if (_windowHandle.IsNull) throw new InvalidOperationException("The hosted window handle is not available.");
        if (!_taskbarWindowLocator.TryRefresh()) throw new InvalidOperationException("The Windows taskbar window could not be found.");

        _originalParentWindow = PInvoke.GetParent(_windowHandle);
        _originalWindowStyle = NativeWindowMethods.GetWindowStyle(_windowHandle);
        PInvoke.SetParent(_windowHandle, _taskbarWindowLocator.TaskbarWindow);
        ApplyHostedWindowStyle();

        IsAttached = true;
        _explorerRestartMonitorService.Start();
        if (deferInitialLayout) CollapseWindowRegion();
    }

    private void ApplyHostedWindowStyle()
    {
        var currentWindowStyle = NativeWindowMethods.GetWindowStyle(_windowHandle);
        var hostedWindowStyle = currentWindowStyle & ~(WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME | WINDOW_STYLE.WS_SYSMENU | WINDOW_STYLE.WS_MINIMIZEBOX | WINDOW_STYLE.WS_MAXIMIZEBOX);
        hostedWindowStyle |= WINDOW_STYLE.WS_CHILD;
        if (currentWindowStyle != hostedWindowStyle) NativeWindowMethods.SetWindowStyle(_windowHandle, hostedWindowStyle);

        RefreshWindowFrame();
    }

    private void RefreshWindowFrame() => PInvoke.SetWindowPos(_windowHandle, HWND.Null, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);

    private void ApplyOrAnimateLayoutSnapshot(TaskbarLayoutSnapshot snapshot)
    {
        if (!CanAnimateLayoutSnapshot(snapshot))
        {
            StopLayoutAnimation();
            ApplyLayoutSnapshot(snapshot);
            return;
        }

        if (_layoutAnimationTimer.IsRunning && AreLayoutSnapshotsClose(snapshot, _layoutAnimationTargetSnapshot)) return;
        if (AreLayoutSnapshotsClose(snapshot, _lastAppliedLayoutSnapshot))
        {
            StopLayoutAnimation();
            ApplyLayoutSnapshot(snapshot);
            return;
        }

        _layoutAnimationStartSnapshot = _lastAppliedLayoutSnapshot;
        _layoutAnimationTargetSnapshot = snapshot;
        _layoutAnimationStartTimestamp = Stopwatch.GetTimestamp();
        _layoutAnimationTimer.Start();
    }

    private bool CanAnimateLayoutSnapshot(TaskbarLayoutSnapshot snapshot)
    {
        if (!_options.AnimateLayoutChanges) return false;
        if (!_hasAppliedLayoutSnapshot) return false;
        if (!_lastAppliedLayoutSnapshot.IsValid || !snapshot.IsValid) return false;
        if (!double.IsFinite(_options.LayoutAnimationDuration) || _options.LayoutAnimationDuration <= 0) return false;
        return AreClose(_lastAppliedLayoutSnapshot.ScaleFactor, snapshot.ScaleFactor);
    }

    private void ApplyLayoutSnapshot(TaskbarLayoutSnapshot snapshot)
    {
        var width = Math.Max(1, RoundDevicePixel(snapshot.Width));
        var height = Math.Max(1, RoundDevicePixel(snapshot.Height));

        _isApplyingLayoutSnapshot = true;
        try { _platformAdapter.ApplyContentBounds(snapshot.AvailableWidth / snapshot.ScaleFactor, snapshot.Width / snapshot.ScaleFactor, snapshot.Height / snapshot.ScaleFactor); }
        finally { _isApplyingLayoutSnapshot = false; }

        _ = PInvoke.SetWindowRgn(_windowHandle, HRGN.Null, true);
        PInvoke.SetWindowPos(_windowHandle, HWND.Null, RoundDevicePixel(snapshot.X), RoundDevicePixel(snapshot.Y), width, height, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        ApplyHostedWindowStyle();

        var region = PInvoke.CreateRectRgn(0, 0, width, height);
        _ = PInvoke.SetWindowRgn(_windowHandle, region, true);
        _lastAppliedLayoutSnapshot = snapshot;
        _hasAppliedLayoutSnapshot = true;
    }

    private void ApplyLayoutAnimationFrame()
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(_layoutAnimationStartTimestamp).TotalMilliseconds;
        var linearProgress = Math.Clamp(elapsedMilliseconds / _options.LayoutAnimationDuration, 0, 1);

        if (linearProgress >= 1)
        {
            StopLayoutAnimation();
            ApplyLayoutSnapshot(_layoutAnimationTargetSnapshot);
            return;
        }

        var easedProgress = GetEasedLayoutAnimationProgress(linearProgress);
        ApplyLayoutSnapshot(InterpolateLayoutSnapshot(_layoutAnimationStartSnapshot, _layoutAnimationTargetSnapshot, easedProgress));
    }

    private double GetEasedLayoutAnimationProgress(double linearProgress)
    {
        var easing = _options.LayoutAnimationEasing;
        if (easing is null) return linearProgress;

        var easedProgress = easing(linearProgress);
        if (!double.IsFinite(easedProgress)) return linearProgress;
        return Math.Clamp(easedProgress, 0, 1);
    }

    private void StopLayoutAnimation()
    {
        if (_layoutAnimationTimer.IsRunning) _layoutAnimationTimer.Stop();
    }

    private void CollapseWindowRegion()
    {
        StopLayoutAnimation();
        _hasAppliedLayoutSnapshot = false;
        var region = PInvoke.CreateRectRgn(0, 0, 0, 0);
        _ = PInvoke.SetWindowRgn(_windowHandle, region, true);
        PInvoke.SetWindowPos(_windowHandle, HWND.Null, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        ApplyHostedWindowStyle();
    }

    private async Task RefreshTaskbarButtonMeasurementAsync()
    {
        var scaleFactor = GetScaleFactor();
        await _taskbarLayoutCalculator.RefreshTaskbarButtonMeasurementAsync(scaleFactor);
    }

    private double GetScaleFactor()
    {
        var dpi = _windowHandle.IsNull ? 0 : PInvoke.GetDpiForWindow(_windowHandle);
        if (dpi == 0 && !_taskbarWindowLocator.TaskbarWindow.IsNull) dpi = PInvoke.GetDpiForWindow(_taskbarWindowLocator.TaskbarWindow);
        if (dpi == 0) return s_lastScaleFactor ?? 1.0;

        var scaleFactor = dpi / 96.0;
        s_lastScaleFactor = scaleFactor;
        return scaleFactor;
    }

    private void OnLayoutRefreshTimerTick() => RefreshLayout();

    private void OnLayoutAnimationTimerTick()
    {
        if (_isDisposed || !IsAttached)
        {
            StopLayoutAnimation();
            return;
        }

        ApplyLayoutAnimationFrame();
    }

    private void OnExplorerRestartMonitorServiceTaskbarWindowRecreated(object? sender, EventArgs e)
    {
        if (_isDisposed || !IsAttached) return;

        _platformAdapter.RunOnDispatcher(() => TaskbarWindowRecreated?.Invoke(this, EventArgs.Empty));
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(TaskbarContentHostBase));
    }

    private static TaskbarLayoutSnapshot InterpolateLayoutSnapshot(TaskbarLayoutSnapshot startSnapshot, TaskbarLayoutSnapshot targetSnapshot, double progress) => new(Interpolate(startSnapshot.X, targetSnapshot.X, progress), Interpolate(startSnapshot.Y, targetSnapshot.Y, progress), Interpolate(startSnapshot.Width, targetSnapshot.Width, progress), Interpolate(startSnapshot.Height, targetSnapshot.Height, progress), Interpolate(startSnapshot.AvailableWidth, targetSnapshot.AvailableWidth, progress), targetSnapshot.ScaleFactor, true);

    private static double Interpolate(double start, double target, double progress) => start + ((target - start) * progress);

    private static int RoundDevicePixel(double value) => (int)Math.Round(value);

    private static bool AreLayoutSnapshotsClose(TaskbarLayoutSnapshot firstSnapshot, TaskbarLayoutSnapshot secondSnapshot) =>
        AreClose(firstSnapshot.X, secondSnapshot.X) && AreClose(firstSnapshot.Y, secondSnapshot.Y) && AreClose(firstSnapshot.Width, secondSnapshot.Width) && AreClose(firstSnapshot.Height, secondSnapshot.Height) && AreClose(firstSnapshot.AvailableWidth, secondSnapshot.AvailableWidth) && AreClose(firstSnapshot.ScaleFactor, secondSnapshot.ScaleFactor) && firstSnapshot.IsValid == secondSnapshot.IsValid;

    private static bool AreClose(double first, double second) => Math.Abs(first - second) < 0.001;
}
