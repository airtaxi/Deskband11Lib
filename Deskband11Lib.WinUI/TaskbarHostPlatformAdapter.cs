using Deskband11Lib.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Deskband11Lib.WinUI;

internal sealed class TaskbarHostPlatformAdapter(Window window, FrameworkElement contentElement, TaskbarContentHostOptions options) : ITaskbarHostPlatformAdapter
{
    private AppWindowPresenterKind _originalPresenter;
    private bool _originalPresenterHasBorder;
    private bool _originalPresenterHasTitleBar;
    private bool _originalPresenterIsResizable;
    private bool _originalPresenterIsMaximizable;
    private bool _originalPresenterIsMinimizable;
    private bool _originalExtendsContentIntoTitleBar;
    private bool _hasPreparedWindow;

    public nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(window);

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

        _originalExtendsContentIntoTitleBar = window.ExtendsContentIntoTitleBar;
        window.ExtendsContentIntoTitleBar = true;

        _originalPresenter = window.AppWindow.Presenter.Kind;
        window.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
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

        _hasPreparedWindow = true;
    }

    public void RestoreWindowAfterChildHosting()
    {
        if (!_hasPreparedWindow) return;

        window.ExtendsContentIntoTitleBar = _originalExtendsContentIntoTitleBar;

        if (_originalPresenter == AppWindowPresenterKind.Overlapped && window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(_originalPresenterHasBorder, _originalPresenterHasTitleBar);
            presenter.IsResizable = _originalPresenterIsResizable;
            presenter.IsMaximizable = _originalPresenterIsMaximizable;
            presenter.IsMinimizable = _originalPresenterIsMinimizable;
        }

        _hasPreparedWindow = false;
    }

    public void ApplyContentBounds(double maxWidth, double width, double height)
    {
        contentElement.MaxWidth = maxWidth;
        contentElement.Width = width;
        contentElement.Height = height;
    }

    public void RunOnDispatcher(Action action) => window.DispatcherQueue.TryEnqueue(() => action());

    public ITaskbarHostTimer CreateTimer(TimeSpan interval, Action tick) => new TaskbarHostTimer(window.DispatcherQueue, interval, tick);
}
