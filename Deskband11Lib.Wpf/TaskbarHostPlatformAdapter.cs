using Deskband11Lib.Core;
using System.Windows;
using System.Windows.Interop;

namespace Deskband11Lib.Wpf;

internal sealed class TaskbarHostPlatformAdapter(Window window, FrameworkElement contentElement, TaskbarContentHostOptions options) : ITaskbarHostPlatformAdapter
{
    private WindowStyle _originalWindowStyle;
    private ResizeMode _originalResizeMode;
    private bool _originalShowInTaskbar;
    private bool _originalShowActivated;
    private bool _originalTopmost;
    private double _originalWidth;
    private double _originalHeight;
    private bool _hasPreparedWindow;

    public nint WindowHandle => new WindowInteropHelper(window).EnsureHandle();

    public double RequestedWidth
    {
        get
        {
            if (options.PreferredWidth > 0) return options.PreferredWidth;
            if (!double.IsNaN(contentElement.Width) && contentElement.Width > 0) return contentElement.Width;
            if (contentElement.ActualWidth > 0) return contentElement.ActualWidth;
            return options.PreferredWidth;
        }
    }

    public double RequestedHeight
    {
        get
        {
            if (options.PreferredHeight > 0) return options.PreferredHeight;
            if (!double.IsNaN(contentElement.Height) && contentElement.Height > 0) return contentElement.Height;
            if (contentElement.ActualHeight > 0) return contentElement.ActualHeight;
            return options.PreferredHeight;
        }
    }

    public void PrepareWindowForChildHosting()
    {
        if (_hasPreparedWindow) return;

        _originalWindowStyle = window.WindowStyle;
        _originalResizeMode = window.ResizeMode;
        _originalShowInTaskbar = window.ShowInTaskbar;
        _originalShowActivated = window.ShowActivated;
        _originalTopmost = window.Topmost;
        _originalWidth = window.Width;
        _originalHeight = window.Height;

        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;

        _hasPreparedWindow = true;
    }

    public void RestoreWindowAfterChildHosting()
    {
        if (!_hasPreparedWindow) return;

        window.WindowStyle = _originalWindowStyle;
        window.ResizeMode = _originalResizeMode;
        window.ShowInTaskbar = _originalShowInTaskbar;
        window.ShowActivated = _originalShowActivated;
        window.Topmost = _originalTopmost;
        window.Width = _originalWidth;
        window.Height = _originalHeight;

        _hasPreparedWindow = false;
    }

    public void ApplyContentBounds(double maxWidth, double width, double height)
    {
        contentElement.MaxWidth = maxWidth;
        contentElement.Width = width;
        contentElement.Height = height;
    }

    public void RunOnDispatcher(Action action)
    {
        if (window.Dispatcher.CheckAccess()) action();
        else window.Dispatcher.BeginInvoke(action);
    }

    public ITaskbarHostTimer CreateTimer(TimeSpan interval, Action tick) => new TaskbarHostTimer(window.Dispatcher, interval, tick);
}
