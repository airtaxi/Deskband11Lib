using Avalonia.Controls;
using Avalonia.Threading;
using Deskband11Lib.Core;

namespace Deskband11Lib.Avalonia;

internal sealed class TaskbarHostPlatformAdapter(Window window, Control contentElement, TaskbarContentHostOptions options) : ITaskbarHostPlatformAdapter
{
    private SystemDecorations _originalSystemDecorations;
    private bool _originalCanResize;
    private bool _originalShowInTaskbar;
    private double _originalWidth;
    private double _originalHeight;
    private bool _hasPreparedWindow;

    public nint WindowHandle => window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    public double RequestedWidth
    {
        get
        {
            if (options.PreferredWidth > 0) return options.PreferredWidth;
            if (!double.IsNaN(contentElement.Width) && contentElement.Width > 0) return contentElement.Width;
            if (contentElement.Bounds.Width > 0) return contentElement.Bounds.Width;
            return options.PreferredWidth;
        }
    }

    public double RequestedHeight
    {
        get
        {
            if (options.PreferredHeight > 0) return options.PreferredHeight;
            if (!double.IsNaN(contentElement.Height) && contentElement.Height > 0) return contentElement.Height;
            if (contentElement.Bounds.Height > 0) return contentElement.Bounds.Height;
            return options.PreferredHeight;
        }
    }

    public void PrepareWindowForChildHosting()
    {
        if (_hasPreparedWindow) return;

        _originalSystemDecorations = window.SystemDecorations;
        _originalCanResize = window.CanResize;
        _originalShowInTaskbar = window.ShowInTaskbar;
        _originalWidth = window.Width;
        _originalHeight = window.Height;

        window.SystemDecorations = SystemDecorations.None;
        window.CanResize = false;
        window.ShowInTaskbar = false;

        _hasPreparedWindow = true;
    }

    public void RestoreWindowAfterChildHosting()
    {
        if (!_hasPreparedWindow) return;

        window.SystemDecorations = _originalSystemDecorations;
        window.CanResize = _originalCanResize;
        window.ShowInTaskbar = _originalShowInTaskbar;
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

    public void RunOnDispatcher(Action action) => Dispatcher.UIThread.Post(action);

    public ITaskbarHostTimer CreateTimer(TimeSpan interval, Action tick) => new TaskbarHostTimer(interval, tick);
}