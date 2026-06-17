using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed partial class ExplorerRestartMonitorService : IDisposable
{
    private const string TaskbarClassName = "Shell_TrayWnd";
    private readonly Lock _gate = new();
    private readonly Timer _pollingTimer;
    private HWND _lastTaskbarWindow;
    private bool _isStarted;
    private bool _isDisposed;

    public ExplorerRestartMonitorService() => _pollingTimer = new Timer(OnPollingTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public event EventHandler? TaskbarWindowRecreated;

    public void Start()
    {
        lock (_gate)
        {
            if (_isDisposed || _isStarted) return;

            _lastTaskbarWindow = PInvoke.FindWindow(TaskbarClassName, null);
            _pollingTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            _isStarted = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_isStarted) return;

            _pollingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _isStarted = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Stop();
        _pollingTimer.Dispose();
        _isDisposed = true;
    }

    private void OnPollingTimerTick(object? state)
    {
        if (_isDisposed) return;

        var taskbarWindow = PInvoke.FindWindow(TaskbarClassName, null);
        if (taskbarWindow.IsNull || taskbarWindow == _lastTaskbarWindow) return;

        _lastTaskbarWindow = taskbarWindow;
        TaskbarWindowRecreated?.Invoke(this, EventArgs.Empty);
    }
}
