using Microsoft.UI.Xaml.Media.Animation;

namespace Deskband11Lib;

public sealed class TaskbarContentHostOptions
{
    public bool AnimateLayoutChanges { get; set; } = true;

    public double LayoutAnimationDuration { get; set; } = 500;

    public EasingFunctionBase? LayoutAnimationEasing { get; set; } = new CubicEase { EasingMode = EasingMode.EaseOut };

    public bool TrackTaskbarButtons { get; set; } = true;

    public bool TrackNotificationArea { get; set; } = true;

    public double StartAreaWidth { get; set; } = 60;

    public double PreferredWidth { get; set; } = 360;

    public double PreferredHeight { get; set; } = 48;

    public TimeSpan LayoutRefreshInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public TaskbarContentPlacement Placement { get; set; } = TaskbarContentPlacement.BeforeNotificationArea;
}
