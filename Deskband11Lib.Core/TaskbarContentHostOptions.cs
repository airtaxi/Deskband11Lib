namespace Deskband11Lib.Core;

public class TaskbarContentHostOptions
{
    public bool AnimateLayoutChanges { get; set; } = true;

    public bool HighRefreshRateMode { get; set; }

    public double LayoutAnimationDuration { get; set; } = 500;

    public Func<double, double>? LayoutAnimationEasing { get; set; } = EasingFunctions.CircleOut;

    public bool TrackTaskbarButtons { get; set; } = true;

    public bool TrackNotificationArea { get; set; } = true;

    public int PreferredMonitorIdentity { get; set; } = 0;

    public double PreferredWidth { get; set; } = 360;

    public double PreferredHeight { get; set; } = 48;

    public TimeSpan LayoutRefreshInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public TaskbarContentPlacement Placement { get; set; } = TaskbarContentPlacement.Auto;
}
