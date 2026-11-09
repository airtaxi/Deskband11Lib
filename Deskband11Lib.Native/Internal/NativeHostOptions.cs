using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Deskband11Lib.Core;

namespace Deskband11Lib.Native.Internal;

// Mirrors db11l_options in include/deskband11lib.h. New fields are only appended, and StructSize tells how many bytes the caller provided.
[StructLayout(LayoutKind.Sequential)]
internal struct NativeHostOptions
{
    public uint StructSize;
    public int AnimateLayoutChanges;
    public int HighRefreshRateMode;
    public NativeEasing LayoutAnimationEasing;
    public int TrackTaskbarButtons;
    public int TrackNotificationArea;
    public int PreferredMonitorIdentity;
    public int AllowFixedSlotResize;
    public TaskbarContentPlacement Placement;
    public uint ManualSlotPriority;
    public double LayoutAnimationDuration;
    public double PreferredWidth;
    public double PreferredHeight;
    public double LayoutRefreshInterval;

    public static NativeHostOptions CreateDefault()
    {
        var defaultOptions = new TaskbarContentHostOptions();
        return new NativeHostOptions
        {
            StructSize = (uint)Unsafe.SizeOf<NativeHostOptions>(),
            AnimateLayoutChanges = ToNativeBoolean(defaultOptions.AnimateLayoutChanges),
            HighRefreshRateMode = ToNativeBoolean(defaultOptions.HighRefreshRateMode),
            LayoutAnimationEasing = NativeEasing.CircleOut,
            TrackTaskbarButtons = ToNativeBoolean(defaultOptions.TrackTaskbarButtons),
            TrackNotificationArea = ToNativeBoolean(defaultOptions.TrackNotificationArea),
            PreferredMonitorIdentity = defaultOptions.PreferredMonitorIdentity,
            AllowFixedSlotResize = ToNativeBoolean(defaultOptions.AllowFixedSlotResize),
            Placement = defaultOptions.Placement,
            ManualSlotPriority = defaultOptions.ManualSlotPriority,
            LayoutAnimationDuration = defaultOptions.LayoutAnimationDuration,
            PreferredWidth = defaultOptions.PreferredWidth,
            PreferredHeight = defaultOptions.PreferredHeight,
            LayoutRefreshInterval = defaultOptions.LayoutRefreshInterval.TotalMilliseconds
        };
    }

    public readonly TaskbarContentHostOptions ToTaskbarContentHostOptions()
    {
        if (PreferredWidth is not > 0) throw new ArgumentException("preferred_width must be greater than zero.");
        if (PreferredHeight is not > 0) throw new ArgumentException("preferred_height must be greater than zero.");
        if (!double.IsFinite(LayoutRefreshInterval) || LayoutRefreshInterval <= 0) throw new ArgumentException("layout_refresh_interval must be a positive number of milliseconds.");
        if (double.IsNaN(LayoutAnimationDuration)) throw new ArgumentException("layout_animation_duration must be a number.");
        if (PreferredMonitorIdentity < 0) throw new ArgumentException("preferred_monitor_identity must not be negative.");
        if (ManualSlotPriority > ushort.MaxValue) throw new ArgumentException("manual_slot_priority must be between 0 and 65535.");
        if (!Enum.IsDefined(Placement)) throw new ArgumentException("placement is not a known db11l_placement value.");
        if (!Enum.IsDefined(LayoutAnimationEasing)) throw new ArgumentException("layout_animation_easing is not a known db11l_easing value.");

        return new TaskbarContentHostOptions
        {
            AnimateLayoutChanges = AnimateLayoutChanges != 0,
            HighRefreshRateMode = HighRefreshRateMode != 0,
            LayoutAnimationDuration = LayoutAnimationDuration,
            LayoutAnimationEasing = ResolveEasing(LayoutAnimationEasing),
            TrackTaskbarButtons = TrackTaskbarButtons != 0,
            TrackNotificationArea = TrackNotificationArea != 0,
            PreferredMonitorIdentity = PreferredMonitorIdentity,
            PreferredWidth = PreferredWidth,
            AllowFixedSlotResize = AllowFixedSlotResize != 0,
            PreferredHeight = PreferredHeight,
            LayoutRefreshInterval = TimeSpan.FromMilliseconds(LayoutRefreshInterval),
            Placement = Placement,
            ManualSlotPriority = (ushort)ManualSlotPriority
        };
    }

    private static int ToNativeBoolean(bool value) => value ? 1 : 0;

    private static Func<double, double> ResolveEasing(NativeEasing easing) => easing switch
    {
        NativeEasing.SineIn => EasingFunctions.SineIn,
        NativeEasing.SineOut => EasingFunctions.SineOut,
        NativeEasing.SineInOut => EasingFunctions.SineInOut,
        NativeEasing.QuadraticIn => EasingFunctions.QuadraticIn,
        NativeEasing.QuadraticOut => EasingFunctions.QuadraticOut,
        NativeEasing.QuadraticInOut => EasingFunctions.QuadraticInOut,
        NativeEasing.CubicIn => EasingFunctions.CubicIn,
        NativeEasing.CubicOut => EasingFunctions.CubicOut,
        NativeEasing.CubicInOut => EasingFunctions.CubicInOut,
        NativeEasing.QuarticIn => EasingFunctions.QuarticIn,
        NativeEasing.QuarticOut => EasingFunctions.QuarticOut,
        NativeEasing.QuarticInOut => EasingFunctions.QuarticInOut,
        NativeEasing.QuinticIn => EasingFunctions.QuinticIn,
        NativeEasing.QuinticOut => EasingFunctions.QuinticOut,
        NativeEasing.QuinticInOut => EasingFunctions.QuinticInOut,
        NativeEasing.ExponentialIn => EasingFunctions.ExponentialIn,
        NativeEasing.ExponentialOut => EasingFunctions.ExponentialOut,
        NativeEasing.ExponentialInOut => EasingFunctions.ExponentialInOut,
        NativeEasing.CircleIn => EasingFunctions.CircleIn,
        NativeEasing.CircleOut => EasingFunctions.CircleOut,
        NativeEasing.CircleInOut => EasingFunctions.CircleInOut,
        _ => EasingFunctions.Linear
    };
}
