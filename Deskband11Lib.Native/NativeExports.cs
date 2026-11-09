using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Deskband11Lib.Core;
using Deskband11Lib.Native.Internal;

namespace Deskband11Lib.Native;

// C entry points declared in include/deskband11lib.h. Every export catches exceptions, because an exception crossing the unmanaged boundary terminates the process.
[SupportedOSPlatform("windows10.0.22000.0")]
internal static unsafe class NativeExports
{
    private const uint AbiVersion = 1;
    private const string NullOutputPointerMessage = "The output pointer must not be null.";

    [UnmanagedCallersOnly(EntryPoint = "db11l_get_abi_version", CallConvs = [typeof(CallConvCdecl)])]
    public static uint GetAbiVersion() => AbiVersion;

    [UnmanagedCallersOnly(EntryPoint = "db11l_get_last_error_message", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetLastErrorMessage(byte* buffer, int bufferSize)
    {
        var message = NativeErrorState.LastErrorMessage;
        var requiredSize = Encoding.UTF8.GetByteCount(message) + 1;
        if (buffer == null || bufferSize < requiredSize) return requiredSize;

        var writtenSize = Encoding.UTF8.GetBytes(message, new Span<byte>(buffer, bufferSize));
        buffer[writtenSize] = 0;
        return requiredSize;
    }

    [UnmanagedCallersOnly(EntryPoint = "db11l_options_init", CallConvs = [typeof(CallConvCdecl)])]
    public static int InitializeOptions(NativeHostOptions* options, uint optionsSize)
    {
        try
        {
            if (options == null || optionsSize < sizeof(uint)) throw new ArgumentException("options must point to a db11l_options of options_size bytes.");

            var defaultOptions = NativeHostOptions.CreateDefault();
            var copySize = Math.Min(optionsSize, (uint)sizeof(NativeHostOptions));
            defaultOptions.StructSize = copySize;
            Buffer.MemoryCopy(&defaultOptions, options, optionsSize, copySize);
            return (int)NativeResult.Ok;
        }
        catch (Exception exception) { return (int)NativeErrorState.Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_create", CallConvs = [typeof(CallConvCdecl)])]
    public static int CreateHost(nint windowHandle, NativeHostOptions* options, delegate* unmanaged[Cdecl]<nint, NativeHostEvent*, void> eventCallback, nint userData, nint* hostHandle)
    {
        try
        {
            if (hostHandle == null) throw new ArgumentException(NullOutputPointerMessage);

            *hostHandle = 0;
            var host = NativeTaskbarContentHost.Create(windowHandle, ReadOptions(options).ToTaskbarContentHostOptions(), eventCallback, userData);
            *hostHandle = NativeHostRegistry.Add(host);
            return (int)NativeResult.Ok;
        }
        catch (Exception exception) { return (int)NativeErrorState.Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_destroy", CallConvs = [typeof(CallConvCdecl)])]
    public static int DestroyHost(nint hostHandle)
    {
        try
        {
            var host = GetHostForCurrentThread(hostHandle);
            NativeHostRegistry.Remove(hostHandle);
            host.Dispose();
            return (int)NativeResult.Ok;
        }
        catch (Exception exception) { return (int)NativeErrorState.Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_attach", CallConvs = [typeof(CallConvCdecl)])]
    public static int AttachHost(nint hostHandle) => InvokeHost(hostHandle, static host => host.Attach());

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_attach_when_layout_ready", CallConvs = [typeof(CallConvCdecl)])]
    public static int AttachHostWhenLayoutReady(nint hostHandle) => InvokeHost(hostHandle, static host => host.BeginAttachWhenLayoutReady());

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_detach", CallConvs = [typeof(CallConvCdecl)])]
    public static int DetachHost(nint hostHandle) => InvokeHost(hostHandle, static host => host.Detach());

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_refresh_layout", CallConvs = [typeof(CallConvCdecl)])]
    public static int RefreshHostLayout(nint hostHandle) => InvokeHost(hostHandle, static host => host.RefreshLayout());

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_poll_event", CallConvs = [typeof(CallConvCdecl)])]
    public static int PollHostEvent(nint hostHandle, NativeHostEvent* nativeEvent)
    {
        try
        {
            if (nativeEvent == null) throw new ArgumentException(NullOutputPointerMessage);
            if (!GetHostForCurrentThread(hostHandle).TryDequeueEvent(out var dequeuedEvent)) return (int)NativeResult.NoEvent;

            *nativeEvent = dequeuedEvent;
            return (int)NativeResult.Ok;
        }
        catch (Exception exception) { return (int)NativeErrorState.Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_is_attached", CallConvs = [typeof(CallConvCdecl)])]
    public static int IsHostAttached(nint hostHandle, int* isAttached) => ReadHostValue(hostHandle, isAttached, static host => host.IsAttached ? 1 : 0);

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_get_taskbar_edge", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetHostTaskbarEdge(nint hostHandle, int* taskbarEdge) => ReadHostValue(hostHandle, taskbarEdge, static host => (int)host.TaskbarEdge);

    [UnmanagedCallersOnly(EntryPoint = "db11l_host_get_taskbar_alignment", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetHostTaskbarAlignment(nint hostHandle, int* taskbarAlignment) => ReadHostValue(hostHandle, taskbarAlignment, static host => (int)host.GetTaskbarAlignment());

    [UnmanagedCallersOnly(EntryPoint = "db11l_is_window_alive", CallConvs = [typeof(CallConvCdecl)])]
    public static int IsWindowAlive(nint windowHandle) => TaskbarWindowHelper.IsWindowAlive(windowHandle) ? 1 : 0;

    private static int InvokeHost(nint hostHandle, Action<NativeTaskbarContentHost> action)
    {
        try
        {
            action(GetHostForCurrentThread(hostHandle));
            return (int)NativeResult.Ok;
        }
        catch (Exception exception) { return (int)NativeErrorState.Fail(exception); }
    }

    private static int ReadHostValue(nint hostHandle, int* value, Func<NativeTaskbarContentHost, int> readValue)
    {
        try
        {
            if (value == null) throw new ArgumentException(NullOutputPointerMessage);

            *value = readValue(GetHostForCurrentThread(hostHandle));
            return (int)NativeResult.Ok;
        }
        catch (Exception exception) { return (int)NativeErrorState.Fail(exception); }
    }

    private static NativeTaskbarContentHost GetHostForCurrentThread(nint hostHandle)
    {
        var host = NativeHostRegistry.Get(hostHandle);
        if (!host.CheckAccess()) throw new NativeResultException(NativeResult.WrongThread, "The host must be used on the thread that created it.");
        return host;
    }

    // Fields the caller did not provide keep their default values, so callers built against an older header remain compatible.
    private static NativeHostOptions ReadOptions(NativeHostOptions* options)
    {
        var resolvedOptions = NativeHostOptions.CreateDefault();
        if (options == null) return resolvedOptions;
        if (options->StructSize < sizeof(uint)) throw new ArgumentException("struct_size must be set, preferably with db11l_options_init.");

        var copySize = Math.Min(options->StructSize, (uint)sizeof(NativeHostOptions));
        Buffer.MemoryCopy(options, &resolvedOptions, sizeof(NativeHostOptions), copySize);
        return resolvedOptions;
    }
}
