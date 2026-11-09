using System.Runtime.InteropServices;
using Deskband11Lib.Core;

namespace Deskband11Lib.Native.Internal;

// Mirrors db11l_event in include/deskband11lib.h.
[StructLayout(LayoutKind.Sequential)]
internal struct NativeHostEvent
{
    public NativeHostEventKind Kind;
    public NativeResult Result;
    public TaskbarEdge TaskbarEdge;
    public int Reserved;
    public double MaxWidth;
    public double Width;
    public double Height;
}
