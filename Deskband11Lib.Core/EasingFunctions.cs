namespace Deskband11Lib.Core;

public static class EasingFunctions
{
    public static double Linear(double progress) => progress;

    public static double SineIn(double progress) => 1 - Math.Cos((progress * Math.PI) / 2);

    public static double SineOut(double progress) => Math.Sin((progress * Math.PI) / 2);

    public static double SineInOut(double progress) => -(Math.Cos(Math.PI * progress) - 1) / 2;

    public static double QuadraticIn(double progress) => progress * progress;

    public static double QuadraticOut(double progress) => 1 - Math.Pow(1 - progress, 2);

    public static double QuadraticInOut(double progress) => progress < 0.5 ? 2 * progress * progress : 1 - (Math.Pow((-2 * progress) + 2, 2) / 2);

    public static double CubicIn(double progress) => progress * progress * progress;

    public static double CubicOut(double progress) => 1 - Math.Pow(1 - progress, 3);

    public static double CubicInOut(double progress) => progress < 0.5 ? 4 * progress * progress * progress : 1 - (Math.Pow((-2 * progress) + 2, 3) / 2);

    public static double QuarticIn(double progress) => progress * progress * progress * progress;

    public static double QuarticOut(double progress) => 1 - Math.Pow(1 - progress, 4);

    public static double QuarticInOut(double progress) => progress < 0.5 ? 8 * progress * progress * progress * progress : 1 - (Math.Pow((-2 * progress) + 2, 4) / 2);

    public static double QuinticIn(double progress) => progress * progress * progress * progress * progress;

    public static double QuinticOut(double progress) => 1 - Math.Pow(1 - progress, 5);

    public static double QuinticInOut(double progress) => progress < 0.5 ? 16 * progress * progress * progress * progress * progress : 1 - (Math.Pow((-2 * progress) + 2, 5) / 2);

    public static double ExponentialIn(double progress) => progress == 0 ? 0 : Math.Pow(2, (10 * progress) - 10);

    public static double ExponentialOut(double progress) => progress == 1 ? 1 : 1 - Math.Pow(2, -10 * progress);

    public static double ExponentialInOut(double progress) => progress == 0 ? 0 : progress == 1 ? 1 : progress < 0.5 ? Math.Pow(2, (20 * progress) - 10) / 2 : (2 - Math.Pow(2, (-20 * progress) + 10)) / 2;

    public static double CircleIn(double progress) => 1 - Math.Sqrt(1 - Math.Pow(progress, 2));

    public static double CircleOut(double progress) => Math.Sqrt(1 - Math.Pow(progress - 1, 2));

    public static double CircleInOut(double progress) => progress < 0.5 ? (1 - Math.Sqrt(1 - Math.Pow(2 * progress, 2))) / 2 : (Math.Sqrt(1 - Math.Pow((-2 * progress) + 2, 2)) + 1) / 2;
}
