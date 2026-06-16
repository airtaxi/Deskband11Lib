using Deskband11Lib.Internal;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Deskband11Lib;

public sealed partial class TaskbarContentHost : IDisposable
{
    private static double? s_lastScaleFactor;
    private readonly Window _window;
    private readonly FrameworkElement _contentElement;
    private readonly TaskbarContentHostOptions _options;
    private readonly TaskbarWindowLocator _taskbarWindowLocator = new();
    private readonly TaskbarButtonReader _taskbarButtonReader = new();
    private readonly ExplorerRestartMonitorService _explorerRestartMonitorService = new();
    private readonly DispatcherQueueTimer _layoutRefreshTimer;
    private readonly TaskbarLayoutCalculator _taskbarLayoutCalculator;
    private HWND _windowHandle;
    private HWND _originalParentWindow;
    private WINDOW_STYLE _originalWindowStyle;
    private bool _originalExtendsContentIntoTitleBar;
    private bool _originalPresenterHasBorder;
    private bool _originalPresenterHasTitleBar;
    private bool _originalPresenterIsResizable;
    private bool _originalPresenterIsMaximizable;
    private bool _originalPresenterIsMinimizable;
    private bool _isDisposed;

    public TaskbarContentHost(Window window, FrameworkElement contentElement) : this(window, contentElement, new TaskbarContentHostOptions()) { }

    public TaskbarContentHost(Window window, FrameworkElement contentElement, TaskbarContentHostOptions? options = null)
    {
        _window = window;
        _contentElement = contentElement;
        _options = options ?? new();
        _taskbarLayoutCalculator = new TaskbarLayoutCalculator(_taskbarWindowLocator, _options, _taskbarButtonReader);
        _layoutRefreshTimer = _window.DispatcherQueue.CreateTimer();
        _layoutRefreshTimer.Interval = _options.LayoutRefreshInterval;
        _layoutRefreshTimer.Tick += OnLayoutRefreshTimerTick;
        _contentElement.SizeChanged += OnContentElementSizeChanged;
        _explorerRestartMonitorService.TaskbarWindowRecreated += OnExplorerRestartMonitorServiceTaskbarWindowRecreated;
    }

    public event EventHandler? TaskbarWindowRecreated;

    public bool IsAttached { get; private set; }

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

    private void AttachCore(bool deferInitialLayout)
    {
        if (IsAttached) return;

        _originalExtendsContentIntoTitleBar = _window.ExtendsContentIntoTitleBar;
        _window.ExtendsContentIntoTitleBar = true;

        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            _originalPresenterHasBorder = presenter.HasBorder;
            _originalPresenterHasTitleBar = presenter.HasTitleBar;
            _originalPresenterIsResizable = presenter.IsResizable;
            _originalPresenterIsMaximizable = presenter.IsMaximizable;
            _originalPresenterIsMinimizable = presenter.IsMinimizable;

            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _windowHandle = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(_window));
        if (_windowHandle.IsNull) throw new InvalidOperationException("The WinUI window handle is not available.");
        if (!_taskbarWindowLocator.TryRefresh()) throw new InvalidOperationException("The Windows taskbar window could not be found.");

        _originalParentWindow = PInvoke.GetParent(_windowHandle);
        _originalWindowStyle = (WINDOW_STYLE)PInvoke.GetWindowLong(_windowHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var childWindowStyle = (_originalWindowStyle & ~WINDOW_STYLE.WS_POPUP) | WINDOW_STYLE.WS_CHILD;

        _ = PInvoke.SetWindowLong(_windowHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)childWindowStyle);
        PInvoke.SetParent(_windowHandle, _taskbarWindowLocator.TaskbarWindow);

        IsAttached = true;
        _explorerRestartMonitorService.Start();
        if (deferInitialLayout) CollapseWindowRegion();
    }

    public void Detach()
    {
        if (!IsAttached) return;

        // Stop background work.
        _layoutRefreshTimer.Stop();
        _explorerRestartMonitorService.Stop();

        // Restore native window state.
        _ = PInvoke.SetWindowRgn(_windowHandle, HRGN.Null, true);
        _ = PInvoke.SetWindowLong(_windowHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)_originalWindowStyle);
        PInvoke.SetParent(_windowHandle, _originalParentWindow);

        // Restore managed window state.
        _window.ExtendsContentIntoTitleBar = _originalExtendsContentIntoTitleBar;

        // Restore presenter state.
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(_originalPresenterHasBorder, _originalPresenterHasTitleBar);
            presenter.IsResizable = _originalPresenterIsResizable;
            presenter.IsMaximizable = _originalPresenterIsMaximizable;
            presenter.IsMinimizable = _originalPresenterIsMinimizable;
        }

        IsAttached = false;
    }

    public void RefreshLayout()
    {
        ThrowIfDisposed();
        if (!IsAttached) return;

        var scaleFactor = GetScaleFactor();
        var snapshot = _taskbarLayoutCalculator.Calculate(GetRequestedWidth(), GetRequestedHeight(), scaleFactor);
        if (!snapshot.IsValid)
        {
            CollapseWindowRegion();
            return;
        }

        _contentElement.MaxWidth = snapshot.AvailableWidth / snapshot.ScaleFactor;
        _contentElement.Width = snapshot.Width / snapshot.ScaleFactor;
        _contentElement.Height = snapshot.Height / snapshot.ScaleFactor;

        _ = PInvoke.SetWindowRgn(_windowHandle, HRGN.Null, true);
        PInvoke.SetWindowPos(_windowHandle, HWND.Null, (int)snapshot.X, (int)snapshot.Y, (int)snapshot.Width, (int)snapshot.Height, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);

        var region = PInvoke.CreateRectRgn(0, 0, (int)snapshot.Width, (int)snapshot.Height);
        _ = PInvoke.SetWindowRgn(_windowHandle, region, true);
    }

    private void CollapseWindowRegion()
    {
        var region = PInvoke.CreateRectRgn(0, 0, 0, 0);
        _ = PInvoke.SetWindowRgn(_windowHandle, region, true);
        PInvoke.SetWindowPos(_windowHandle, HWND.Null, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _contentElement.SizeChanged -= OnContentElementSizeChanged;
        _explorerRestartMonitorService.TaskbarWindowRecreated -= OnExplorerRestartMonitorServiceTaskbarWindowRecreated;
        TaskbarWindowRecreated = null;

        Detach();

        _explorerRestartMonitorService.Dispose();
        _taskbarButtonReader.Dispose();

        _isDisposed = true;
    }

    private double GetRequestedWidth()
    {
        if (_options.PreferredWidth > 0) return _options.PreferredWidth;
        if (!double.IsNaN(_contentElement.Width) && _contentElement.Width > 0) return _contentElement.Width;
        if (_contentElement.ActualWidth > 0) return _contentElement.ActualWidth;
        return _options.PreferredWidth;
    }

    private double GetRequestedHeight()
    {
        if (_options.PreferredHeight > 0) return _options.PreferredHeight;
        if (!double.IsNaN(_contentElement.Height) && _contentElement.Height > 0) return _contentElement.Height;
        if (_contentElement.ActualHeight > 0) return _contentElement.ActualHeight;
        return _options.PreferredHeight;
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

    private void OnContentElementSizeChanged(object sender, SizeChangedEventArgs e) => RefreshLayout();

    private void OnLayoutRefreshTimerTick(DispatcherQueueTimer sender, object e) => RefreshLayout();

    private void OnExplorerRestartMonitorServiceTaskbarWindowRecreated(object? sender, EventArgs e)
    {
        if (_isDisposed || !IsAttached) return;

        _window.DispatcherQueue.TryEnqueue(() => TaskbarWindowRecreated?.Invoke(this, EventArgs.Empty));
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(TaskbarContentHost));
    }
}
