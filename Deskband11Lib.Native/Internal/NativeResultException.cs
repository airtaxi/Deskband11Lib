namespace Deskband11Lib.Native.Internal;

internal sealed class NativeResultException(NativeResult result, string message) : Exception(message)
{
    public NativeResult Result { get; } = result;
}
