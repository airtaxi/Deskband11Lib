namespace Deskband11Lib.Native.Internal;

// Hands out opaque integer handles so that a stale or invalid handle from the caller is rejected instead of being dereferenced.
internal static class NativeHostRegistry
{
    private static readonly Lock s_lock = new();
    private static readonly Dictionary<nint, NativeTaskbarContentHost> s_hosts = [];
    private static nint s_lastHostHandle;

    public static nint Add(NativeTaskbarContentHost host)
    {
        lock (s_lock)
        {
            var hostHandle = ++s_lastHostHandle;
            s_hosts.Add(hostHandle, host);
            return hostHandle;
        }
    }

    public static NativeTaskbarContentHost Get(nint hostHandle)
    {
        lock (s_lock)
        {
            if (s_hosts.TryGetValue(hostHandle, out var host)) return host;
            throw new NativeResultException(NativeResult.InvalidHandle, "The host handle is not valid.");
        }
    }

    public static void Remove(nint hostHandle)
    {
        lock (s_lock)
        {
            s_hosts.Remove(hostHandle);
        }
    }
}
