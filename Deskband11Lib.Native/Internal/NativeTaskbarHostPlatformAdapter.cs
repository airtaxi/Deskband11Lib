using Deskband11Lib.Core;
using Deskband11Lib.Native.Internal.Win32;

namespace Deskband11Lib.Native.Internal;

internal sealed class NativeTaskbarHostPlatformAdapter(nint windowHandle, TaskbarContentHostOptions options, MessageWindowDispatcher dispatcher) : ITaskbarHostPlatformAdapter
{
    private const uint RefreshFrameFlags = NativeWin32Methods.SetWindowPositionFrameChanged | NativeWin32Methods.SetWindowPositionNoActivate | NativeWin32Methods.SetWindowPositionNoMove | NativeWin32Methods.SetWindowPositionNoSize | NativeWin32Methods.SetWindowPositionNoZOrder;

    public event Action<double, double, double>? ContentBoundsApplied;

    public nint WindowHandle => windowHandle;

    public double RequestedWidth => options.PreferredWidth;

    public double RequestedHeight => options.PreferredHeight;

    // The hosting framework owns the window state, so the caller removes the frame before attaching.
    public void PrepareWindowForChildHosting() { }

    // Core restores the original parent and style, so only the non-client frame needs to be recalculated.
    public void RestoreWindowAfterChildHosting() => _ = NativeWin32Methods.SetWindowPos(windowHandle, 0, 0, 0, 0, 0, RefreshFrameFlags);

    public void ApplyContentBounds(double maxWidth, double width, double height) => ContentBoundsApplied?.Invoke(maxWidth, width, height);

    public void RunOnDispatcher(Action action) => dispatcher.Post(action);

    public ITaskbarHostTimer CreateTimer(TimeSpan interval, Action tick) => new MessageWindowTimer(dispatcher, interval, tick);
}
