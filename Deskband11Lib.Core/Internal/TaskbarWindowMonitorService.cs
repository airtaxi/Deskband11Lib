using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal sealed partial class TaskbarWindowMonitorService : IDisposable
{
    private const string PrimaryTaskbarClassName = "Shell_TrayWnd";

    private const string SecondaryTaskbarClassName = "Shell_SecondaryTrayWnd";

    private readonly Func<int> _getEffectiveMonitorIdentity;
    private readonly Func<int> _getPreferredMonitorIdentity;
    private readonly Action<int> _setEffectiveMonitorIdentity;
    private readonly Lock _gate = new();
    private readonly Timer _pollingTimer;
    private HWND _lastTaskbarWindow;
    private bool _isStarted;
    private bool _isDisposed;

    public TaskbarWindowMonitorService(Func<int> getEffectiveMonitorIdentity, Func<int> getPreferredMonitorIdentity, Action<int> setEffectiveMonitorIdentity)
    {
        _getEffectiveMonitorIdentity = getEffectiveMonitorIdentity;
        _getPreferredMonitorIdentity = getPreferredMonitorIdentity;
        _setEffectiveMonitorIdentity = setEffectiveMonitorIdentity;
        _pollingTimer = new Timer(OnPollingTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler? TaskbarWindowRecreated;

    public event EventHandler? PreferredMonitorRestored;

    public void Start()
    {
        lock (_gate)
        {
            if (_isDisposed || _isStarted) return;

            _lastTaskbarWindow = ResolveTaskbarWindow();
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

        if (TryRestorePreferredMonitor()) return;

        var taskbarWindow = ResolveTaskbarWindow();
        if (taskbarWindow == _lastTaskbarWindow) return;

        _lastTaskbarWindow = taskbarWindow;
        TaskbarWindowRecreated?.Invoke(this, EventArgs.Empty);
    }

    private bool TryRestorePreferredMonitor()
    {
        var preferredMonitorIdentity = _getPreferredMonitorIdentity();
        if (preferredMonitorIdentity <= 0) return false;
        if (_getEffectiveMonitorIdentity() == preferredMonitorIdentity) return false;

        var previousWindow = HWND.Null;
        var remaining = preferredMonitorIdentity;
        while (true)
        {
            previousWindow = PInvoke.FindWindowEx(HWND.Null, previousWindow, SecondaryTaskbarClassName, null);
            if (previousWindow.IsNull) return false;

            remaining--;
            if (remaining == 0)
            {
                _setEffectiveMonitorIdentity(preferredMonitorIdentity);
                _lastTaskbarWindow = previousWindow;
                PreferredMonitorRestored?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
    }

    private HWND ResolveTaskbarWindow()
    {
        var effectiveMonitorIdentity = _getEffectiveMonitorIdentity();
        if (effectiveMonitorIdentity <= 0) return PInvoke.FindWindow(PrimaryTaskbarClassName, null);

        var previousWindow = HWND.Null;
        while (true)
        {
            previousWindow = PInvoke.FindWindowEx(HWND.Null, previousWindow, SecondaryTaskbarClassName, null);
            if (previousWindow.IsNull) break;

            effectiveMonitorIdentity--;
            if (effectiveMonitorIdentity == 0) return previousWindow;
        }

        return HWND.Null;
    }
}