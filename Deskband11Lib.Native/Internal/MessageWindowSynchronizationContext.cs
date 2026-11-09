namespace Deskband11Lib.Native.Internal;

internal sealed class MessageWindowSynchronizationContext(MessageWindowDispatcher dispatcher) : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => dispatcher.Post(() => callback(state));

    public override void Send(SendOrPostCallback callback, object? state)
    {
        if (!dispatcher.CheckAccess()) throw new NotSupportedException("Synchronous dispatch from another thread is not supported.");
        callback(state);
    }

    public override SynchronizationContext CreateCopy() => this;
}
