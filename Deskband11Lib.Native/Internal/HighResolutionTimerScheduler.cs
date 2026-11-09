using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Deskband11Lib.Native.Internal.Interop;

namespace Deskband11Lib.Native.Internal;

// SetTimer only fires on the system timer tick (15.625 ms by default), so short animation intervals are rounded up to whole ticks.
// A background thread waits on a high resolution waitable timer instead and posts each due tick to the message window, so ticks still run on the host thread.
internal sealed unsafe class HighResolutionTimerScheduler(nint windowHandle, uint tickMessage) : IDisposable
{
    private static readonly long s_minimumIntervalTimestamp = Stopwatch.Frequency / 1000;

    private readonly Lock _lock = new();
    private readonly Dictionary<nuint, ScheduledTimer> _timers = [];
    private nint _wakeEventHandle;
    private nint _waitableTimerHandle;
    private bool _isThreadStarted;
    private bool _isDisposed;

    public void Start(nuint timerIdentifier, TimeSpan interval)
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            EnsureThreadStarted();
            if (!_timers.TryGetValue(timerIdentifier, out var timer)) _timers[timerIdentifier] = timer = new ScheduledTimer();
            timer.IntervalTimestamp = Math.Max(s_minimumIntervalTimestamp, (long)(interval.TotalSeconds * Stopwatch.Frequency));
            timer.NextDueTimestamp = Stopwatch.GetTimestamp() + timer.IntervalTimestamp;
            timer.IsActive = true;
            _ = NativeWin32Methods.SetEvent(_wakeEventHandle);
        }
    }

    public void Stop(nuint timerIdentifier)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(timerIdentifier, out var timer))
            {
                timer.IsActive = false;
            }
        }
    }

    public void Remove(nuint timerIdentifier)
    {
        lock (_lock)
        {
            _timers.Remove(timerIdentifier);
        }
    }

    // Clears the pending flag of a delivered tick and returns whether the timer is still running.
    public bool CompleteTick(nuint timerIdentifier)
    {
        lock (_lock)
        {
            if (!_timers.TryGetValue(timerIdentifier, out var timer)) return false;

            timer.IsTickPending = false;
            return timer.IsActive;
        }
    }

    // Ticks are posted while holding the lock, so no tick reaches the window after this returns and the window can be destroyed safely.
    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _timers.Clear();
            if (_isThreadStarted) _ = NativeWin32Methods.SetEvent(_wakeEventHandle);
        }
    }

    private void EnsureThreadStarted()
    {
        if (_isThreadStarted) return;

        var wakeEventHandle = NativeWin32Methods.CreateEvent(null, 0, 0, null);
        if (wakeEventHandle == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());

        var waitableTimerHandle = NativeWin32Methods.CreateWaitableTimerEx(null, null, NativeWin32Methods.CreateWaitableTimerHighResolutionFlag, NativeWin32Methods.TimerAllAccess);
        if (waitableTimerHandle == 0)
        {
            var errorCode = Marshal.GetLastPInvokeError();
            _ = NativeWin32Methods.CloseHandle(wakeEventHandle);
            throw new Win32Exception(errorCode);
        }

        _wakeEventHandle = wakeEventHandle;
        _waitableTimerHandle = waitableTimerHandle;
        new Thread(RunSchedulerLoop) { IsBackground = true, Name = "Deskband11Lib.Native timer", Priority = ThreadPriority.Highest }.Start();
        _isThreadStarted = true;
    }

    // The thread owns both handles after it starts and closes them once the scheduler is disposed.
    private void RunSchedulerLoop()
    {
        nint* waitHandles = stackalloc nint[2];
        waitHandles[0] = _wakeEventHandle;
        waitHandles[1] = _waitableTimerHandle;

        while (TryPostDueTicks(out var waitTime))
        {
            if (waitTime < 0) _ = NativeWin32Methods.WaitForSingleObject(_wakeEventHandle, NativeWin32Methods.Infinite);
            else
            {
                // Negative due times are relative, in 100 ns units.
                var dueTime = -Math.Max(1, waitTime);
                _ = NativeWin32Methods.SetWaitableTimer(_waitableTimerHandle, &dueTime, 0, 0, null, 0);
                _ = NativeWin32Methods.WaitForMultipleObjects(2, waitHandles, 0, NativeWin32Methods.Infinite);
            }
        }

        _ = NativeWin32Methods.CancelWaitableTimer(_waitableTimerHandle);
        _ = NativeWin32Methods.CloseHandle(_waitableTimerHandle);
        _ = NativeWin32Methods.CloseHandle(_wakeEventHandle);
    }

    // Posts the ticks that are due and returns the wait in 100 ns units until the nearest next tick, or -1 when no timer is running.
    private bool TryPostDueTicks(out long waitTime)
    {
        lock (_lock)
        {
            waitTime = -1;
            if (_isDisposed) return false;

            var currentTimestamp = Stopwatch.GetTimestamp();
            var nearestDueTimestamp = long.MaxValue;
            foreach (var (timerIdentifier, timer) in _timers)
            {
                if (!timer.IsActive) continue;

                if (timer.NextDueTimestamp <= currentTimestamp)
                {
                    // At most one tick per timer waits in the message queue, so a busy host thread drops frames instead of falling behind.
                    if (!timer.IsTickPending) timer.IsTickPending = NativeWin32Methods.PostMessage(windowHandle, tickMessage, (nint)timerIdentifier, 0) != 0;

                    // The next tick follows the schedule rather than the actual wake time, so small wake delays do not accumulate.
                    timer.NextDueTimestamp += timer.IntervalTimestamp;
                    if (timer.NextDueTimestamp <= currentTimestamp) timer.NextDueTimestamp = currentTimestamp + timer.IntervalTimestamp;
                }

                nearestDueTimestamp = Math.Min(nearestDueTimestamp, timer.NextDueTimestamp);
            }

            if (nearestDueTimestamp != long.MaxValue) waitTime = Stopwatch.GetElapsedTime(currentTimestamp, nearestDueTimestamp).Ticks;
            return true;
        }
    }

    private sealed class ScheduledTimer
    {
        public long IntervalTimestamp;
        public long NextDueTimestamp;
        public bool IsActive;
        public bool IsTickPending;
    }
}
