using Deskband11Lib.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Reflection;
using Uno.UI.NativeElementHosting;
using Uno.UI.Xaml;
using System.Runtime.Versioning;

namespace Deskband11Lib.Uno.Skia;

[SupportedOSPlatform("windows10.0.22000.0")]
internal sealed class TaskbarHostPlatformAdapter(Window window, FrameworkElement contentElement, TaskbarContentHostOptions options) : ITaskbarHostPlatformAdapter
{
    private bool _originalExtendsContentIntoTitleBar;
    private bool _originalPresenterHasBorder;
    private bool _originalPresenterHasTitleBar;
    private bool _originalPresenterIsResizable;
    private bool _originalPresenterIsMaximizable;
    private bool _originalPresenterIsMinimizable;
    private bool _hasPreparedWindow;

    public nint WindowHandle
    {
        get
        {
            var nativeWindow = WindowHelper.GetNativeWindow(window);
            if (nativeWindow is Win32NativeWindow win32Native) return win32Native.Hwnd;
            if (TryGetWpfWindowHandle(nativeWindow, out var wpfWindowHandle)) return wpfWindowHandle;

            throw new PlatformNotSupportedException("Deskband11Lib.Uno.Skia requires an Uno Skia Win32 or WPF host. The current platform does not provide a Win32 window handle.");
        }
    }

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

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
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

    private static bool TryGetWpfWindowHandle(object? nativeWindow, out nint windowHandle)
    {
        windowHandle = 0;
        if (nativeWindow is null) return false;

        var windowInteropHelperType = Type.GetType("System.Windows.Interop.WindowInteropHelper, PresentationFramework") ?? AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("System.Windows.Interop.WindowInteropHelper", false)).FirstOrDefault(type => type is not null);
        if (windowInteropHelperType is null) return false;

        var constructor = windowInteropHelperType.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(constructorInfo => constructorInfo.GetParameters() is [{ } parameter] && parameter.ParameterType.IsInstanceOfType(nativeWindow));
        if (constructor is null) return false;

        var helper = constructor.Invoke([nativeWindow]);
        var ensureHandleMethod = windowInteropHelperType.GetMethod("EnsureHandle", BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);
        if (ensureHandleMethod?.Invoke(helper, null) is nint ensuredWindowHandle && ensuredWindowHandle != 0)
        {
            windowHandle = ensuredWindowHandle;
            return true;
        }

        var handleProperty = windowInteropHelperType.GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public);
        if (handleProperty?.GetValue(helper) is nint existingWindowHandle && existingWindowHandle != 0)
        {
            windowHandle = existingWindowHandle;
            return true;
        }

        return false;
    }
}
