using System.ComponentModel;

namespace Deskband11Lib.Native.Internal;

internal static class NativeErrorState
{
    [ThreadStatic]
    private static string? s_lastErrorMessage;

    public static string LastErrorMessage => s_lastErrorMessage ?? string.Empty;

    public static void SetLastErrorMessage(string message) => s_lastErrorMessage = message;

    public static NativeResult Fail(Exception exception)
    {
        s_lastErrorMessage = exception.Message;
        return GetResult(exception);
    }

    public static NativeResult GetResult(Exception exception) => exception switch
    {
        NativeResultException nativeResultException => nativeResultException.Result,
        ArgumentException => NativeResult.InvalidArgument,
        InvalidOperationException => NativeResult.InvalidState,
        Win32Exception => NativeResult.Win32Error,
        _ => NativeResult.Unexpected
    };
}
