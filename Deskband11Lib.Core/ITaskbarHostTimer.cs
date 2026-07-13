namespace Deskband11Lib.Core;

public interface ITaskbarHostTimer : IDisposable
{
    bool IsRunning { get; }

    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}
