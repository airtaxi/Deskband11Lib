namespace Deskband11Lib.Core;

public interface ITaskbarHostPlatformAdapter
{
    nint WindowHandle { get; }

    double RequestedWidth { get; }

    double RequestedHeight { get; }

    void PrepareWindowForChildHosting();

    void RestoreWindowAfterChildHosting();

    void ApplyContentBounds(double maxWidth, double width, double height);

    void RunOnDispatcher(Action action);

    ITaskbarHostTimer CreateTimer(TimeSpan interval, Action tick);
}
