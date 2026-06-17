using Deskband11Lib.Core;
using System.Windows;
using System.Runtime.Versioning;

namespace Deskband11Lib.Wpf;

[SupportedOSPlatform("windows10.0.22000.0")]
public sealed partial class TaskbarContentHost : TaskbarContentHostBase
{
    private readonly FrameworkElement _contentElement;

    public TaskbarContentHost(Window window, FrameworkElement contentElement) : this(window, contentElement, null) { }

    public TaskbarContentHost(Window window, FrameworkElement contentElement, TaskbarContentHostOptions? options = null) : this(contentElement, CreateConstructionState(window, contentElement, options)) { }

    private TaskbarContentHost(FrameworkElement contentElement, TaskbarContentHostConstructionState constructionState) : base(constructionState.PlatformAdapter, constructionState.Options)
    {
        _contentElement = contentElement;
        _contentElement.SizeChanged += OnContentElementSizeChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _contentElement.SizeChanged -= OnContentElementSizeChanged;
        base.Dispose(disposing);
    }

    private void OnContentElementSizeChanged(object sender, SizeChangedEventArgs e) => NotifyContentSizeChanged();

    private static TaskbarContentHostConstructionState CreateConstructionState(Window window, FrameworkElement contentElement, TaskbarContentHostOptions? options)
    {
        var resolvedOptions = options ?? new TaskbarContentHostOptions();
        return new TaskbarContentHostConstructionState(new TaskbarHostPlatformAdapter(window, contentElement, resolvedOptions), resolvedOptions);
    }

    private sealed record TaskbarContentHostConstructionState(TaskbarHostPlatformAdapter PlatformAdapter, TaskbarContentHostOptions Options);
}
