namespace Deskband11Lib.Core;

public interface ITaskbarHostTimer : IDisposable
{
    bool IsRunning { get; }

    void Start();

    void Stop();
}
