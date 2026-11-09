import { loadLibrary, type NativeOptions } from './bindings';
import type { Easing, TaskbarContentPlacement } from './enums';
import { checkResult } from './errors';

/** A preferredWidth that fills the space left after fixed-width hosts are served. */
export const STRETCH = Number.MAX_VALUE;

/**
 * Options for a TaskbarContentHost. Sizes are in effective pixels and durations in milliseconds.
 * Options that are left out keep the defaults of the native library.
 *
 * preferredWidth is the length along the taskbar and preferredHeight the size across it, so on a left or
 * right taskbar they map to the height and the width of the window.
 */
export interface TaskbarContentHostOptions {
    /** Length along the taskbar. Defaults to 360. Use STRETCH to fill the space left by fixed-width hosts. */
    preferredWidth?: number;
    /** Size across the taskbar, capped by its thickness. Defaults to 48. */
    preferredHeight?: number;
    /** Defaults to TaskbarContentPlacement.Auto. */
    placement?: TaskbarContentPlacement;
    /** 0 is the primary taskbar. 1 and above are secondary taskbars ordered from left to right. Defaults to 0. */
    preferredMonitorIdentity?: number;
    /** 0 to 65535. Lower values are placed first when several hosts share an area. Defaults to 65535, which orders by arrival. */
    manualSlotPriority?: number;
    /** Lets fixed-width hosts shrink proportionally when the available space runs out. Defaults to true. */
    allowFixedSlotResize?: boolean;
    /** Defaults to true. */
    animateLayoutChanges?: boolean;
    /** Matches the animation frame interval to the refresh rate of the monitor. Defaults to true. */
    highRefreshRateMode?: boolean;
    /** Defaults to 500. */
    layoutAnimationDuration?: number;
    /** Defaults to Easing.CircleOut. */
    layoutAnimationEasing?: Easing;
    /** Measures the taskbar buttons through UI Automation so the window never covers them. Defaults to true. */
    trackTaskbarButtons?: boolean;
    /** Keeps the window away from the notification area. Defaults to true. */
    trackNotificationArea?: boolean;
    /** Milliseconds between layout refreshes. Defaults to 500. */
    layoutRefreshInterval?: number;
    /** Milliseconds between checks for host events. Defaults to 50. */
    eventPollInterval?: number;
}

export function toNativeOptions(options: TaskbarContentHostOptions): NativeOptions {
    if (options.manualSlotPriority !== undefined && !(options.manualSlotPriority >= 0 && options.manualSlotPriority <= 65535)) throw new RangeError('manualSlotPriority must be between 0 and 65535.');

    const library = loadLibrary();
    const nativeOptions = {} as NativeOptions;
    checkResult(library.initializeOptions(nativeOptions, library.optionsSize));

    if (options.preferredWidth !== undefined) nativeOptions.preferred_width = options.preferredWidth;
    if (options.preferredHeight !== undefined) nativeOptions.preferred_height = options.preferredHeight;
    if (options.placement !== undefined) nativeOptions.placement = options.placement;
    if (options.preferredMonitorIdentity !== undefined) nativeOptions.preferred_monitor_identity = options.preferredMonitorIdentity;
    if (options.manualSlotPriority !== undefined) nativeOptions.manual_slot_priority = options.manualSlotPriority;
    if (options.allowFixedSlotResize !== undefined) nativeOptions.allow_fixed_slot_resize = toNativeBoolean(options.allowFixedSlotResize);
    if (options.animateLayoutChanges !== undefined) nativeOptions.animate_layout_changes = toNativeBoolean(options.animateLayoutChanges);
    if (options.highRefreshRateMode !== undefined) nativeOptions.high_refresh_rate_mode = toNativeBoolean(options.highRefreshRateMode);
    if (options.layoutAnimationDuration !== undefined) nativeOptions.layout_animation_duration = options.layoutAnimationDuration;
    if (options.layoutAnimationEasing !== undefined) nativeOptions.layout_animation_easing = options.layoutAnimationEasing;
    if (options.trackTaskbarButtons !== undefined) nativeOptions.track_taskbar_buttons = toNativeBoolean(options.trackTaskbarButtons);
    if (options.trackNotificationArea !== undefined) nativeOptions.track_notification_area = toNativeBoolean(options.trackNotificationArea);
    if (options.layoutRefreshInterval !== undefined) nativeOptions.layout_refresh_interval = options.layoutRefreshInterval;
    return nativeOptions;
}

function toNativeBoolean(value: boolean): number {
    return value ? 1 : 0;
}
