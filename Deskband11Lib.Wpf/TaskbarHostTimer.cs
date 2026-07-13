using Deskband11Lib.Core;
using System.Windows.Threading;

namespace Deskband11Lib.Wpf;

internal sealed class TaskbarHostTimer : ITaskbarHostTimer
{
    private readonly DispatcherTimer _timer;
    private readonly Action _tick;

    public TaskbarHostTimer(Dispatcher dispatcher, TimeSpan interval, Action tick)
    {
        _tick = tick;
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = interval };
        _timer.Tick += OnTimerTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    public TimeSpan Interval { get => _timer.Interval; set => _timer.Interval = value; }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private void OnTimerTick(object? sender, EventArgs e) => _tick();
}
