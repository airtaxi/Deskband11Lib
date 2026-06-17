using Deskband11Lib.Core;
using Microsoft.UI.Dispatching;

namespace Deskband11Lib.Uno.Skia;

internal sealed class TaskbarHostTimer : ITaskbarHostTimer
{
    private readonly DispatcherQueueTimer _timer;
    private readonly Action _tick;

    public TaskbarHostTimer(DispatcherQueue dispatcherQueue, TimeSpan interval, Action tick)
    {
        _tick = tick;
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = interval;
        _timer.Tick += OnTimerTick;
    }

    public bool IsRunning => _timer.IsRunning;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object e) => _tick();
}
