using Avalonia.Threading;
using Deskband11Lib.Core;

namespace Deskband11Lib.Avalonia;

internal sealed class TaskbarHostTimer : ITaskbarHostTimer
{
    private readonly DispatcherTimer _timer;
    private readonly Action _tick;

    public TaskbarHostTimer(TimeSpan interval, Action tick)
    {
        _tick = tick;
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += OnTimerTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private void OnTimerTick(object? sender, EventArgs e) => _tick();
}