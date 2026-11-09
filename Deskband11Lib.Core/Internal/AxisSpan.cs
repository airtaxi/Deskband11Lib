namespace Deskband11Lib.Core.Internal;

internal readonly record struct AxisSpan(int Start, int End)
{
    public static AxisSpan Invalid => new(0, 0);

    public bool IsValid => End > Start;

    public int Length => Math.Max(0, End - Start);
}
