using Deskband11Lib.Core;

namespace Deskband11Lib.Native.Internal;

internal sealed class MessageWindowTimer : ITaskbarHostTimer
{
    private readonly MessageWindowDispatcher _dispatcher;
    private readonly Action _tick;
    private readonly nuint _timerIdentifier;
    private TimeSpan _interval;

    public MessageWindowTimer(MessageWindowDispatcher dispatcher, TimeSpan interval, Action tick)
    {
        _dispatcher = dispatcher;
        _tick = tick;
        _interval = interval;
        _timerIdentifier = dispatcher.RegisterTimer(OnDispatcherTimerTick);
    }

    public bool IsRunning { get; private set; }

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value;
            if (IsRunning) _dispatcher.StartTimer(_timerIdentifier, value);
        }
    }

    public void Start()
    {
        _dispatcher.StartTimer(_timerIdentifier, _interval);
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
        _dispatcher.StopTimer(_timerIdentifier);
    }

    public void Dispose()
    {
        IsRunning = false;
        _dispatcher.UnregisterTimer(_timerIdentifier);
    }

    // A tick that was already queued when the timer stopped is ignored.
    private void OnDispatcherTimerTick()
    {
        if (IsRunning)
        {
            _tick();
        }
    }
}
