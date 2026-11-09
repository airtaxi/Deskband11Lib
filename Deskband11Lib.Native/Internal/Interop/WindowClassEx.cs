using System.Runtime.InteropServices;

namespace Deskband11Lib.Native.Internal.Interop;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WindowClassEx
{
    public uint Size;
    public uint Style;
    public delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint> WindowProcedure;
    public int ClassExtraByteCount;
    public int WindowExtraByteCount;
    public nint InstanceHandle;
    public nint IconHandle;
    public nint CursorHandle;
    public nint BackgroundBrushHandle;
    public char* MenuName;
    public char* ClassName;
    public nint SmallIconHandle;
}
