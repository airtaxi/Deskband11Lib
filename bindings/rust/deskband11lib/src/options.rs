use std::time::Duration;

use crate::bindings::{NativeLibrary, NativeOptions, check};
use crate::enums::{Easing, TaskbarContentPlacement};
use crate::error::Result;

/// A `preferred_width` that fills the space left after fixed-width hosts are served.
pub const STRETCH: f64 = f64::MAX;

/// Options for a [`TaskbarContentHost`](crate::TaskbarContentHost). Sizes are in effective pixels.
///
/// Options left as `None` keep the defaults of the native library, shown in parentheses.
/// `preferred_width` is the length along the taskbar and `preferred_height` the size across it, so on a left or
/// right taskbar they map to the height and the width of the window.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct TaskbarContentHostOptions {
    /// Length along the taskbar (360). Use [`STRETCH`] to fill the space left by fixed-width hosts.
    pub preferred_width: Option<f64>,
    /// Size across the taskbar, capped by its thickness (48).
    pub preferred_height: Option<f64>,
    /// Where the window is placed along the taskbar (`Auto`).
    pub placement: Option<TaskbarContentPlacement>,
    /// 0 is the primary taskbar. 1 and above are secondary taskbars ordered from left to right (0).
    pub preferred_monitor_identity: Option<i32>,
    /// Lower values are placed first when several hosts share an area. 65535 orders by arrival (65535).
    pub manual_slot_priority: Option<u16>,
    /// Lets fixed-width hosts shrink proportionally when the available space runs out (`true`).
    pub allow_fixed_slot_resize: Option<bool>,
    /// Animates position and size changes (`true`).
    pub animate_layout_changes: Option<bool>,
    /// Matches the animation frame interval to the refresh rate of the monitor (`true`).
    pub high_refresh_rate_mode: Option<bool>,
    /// Duration of the layout animation (500 ms).
    pub layout_animation_duration: Option<Duration>,
    /// Easing of the layout animation (`CircleOut`).
    pub layout_animation_easing: Option<Easing>,
    /// Measures the taskbar buttons through UI Automation so the window never covers them (`true`).
    pub track_taskbar_buttons: Option<bool>,
    /// Keeps the window away from the notification area (`true`).
    pub track_notification_area: Option<bool>,
    /// Interval between layout refreshes (500 ms).
    pub layout_refresh_interval: Option<Duration>,
}

impl TaskbarContentHostOptions {
    pub(crate) fn to_native(&self, library: &NativeLibrary) -> Result<NativeOptions> {
        let mut native_options = NativeOptions::default();
        check(library, unsafe {
            (library.initialize_options)(&mut native_options, size_of::<NativeOptions>() as u32)
        })?;

        if let Some(value) = self.preferred_width {
            native_options.preferred_width = value;
        }
        if let Some(value) = self.preferred_height {
            native_options.preferred_height = value;
        }
        if let Some(value) = self.placement {
            native_options.placement = value as i32;
        }
        if let Some(value) = self.preferred_monitor_identity {
            native_options.preferred_monitor_identity = value;
        }
        if let Some(value) = self.manual_slot_priority {
            native_options.manual_slot_priority = u32::from(value);
        }
        if let Some(value) = self.allow_fixed_slot_resize {
            native_options.allow_fixed_slot_resize = i32::from(value);
        }
        if let Some(value) = self.animate_layout_changes {
            native_options.animate_layout_changes = i32::from(value);
        }
        if let Some(value) = self.high_refresh_rate_mode {
            native_options.high_refresh_rate_mode = i32::from(value);
        }
        if let Some(value) = self.layout_animation_duration {
            native_options.layout_animation_duration = value.as_secs_f64() * 1000.0;
        }
        if let Some(value) = self.layout_animation_easing {
            native_options.layout_animation_easing = value as i32;
        }
        if let Some(value) = self.track_taskbar_buttons {
            native_options.track_taskbar_buttons = i32::from(value);
        }
        if let Some(value) = self.track_notification_area {
            native_options.track_notification_area = i32::from(value);
        }
        if let Some(value) = self.layout_refresh_interval {
            native_options.layout_refresh_interval = value.as_secs_f64() * 1000.0;
        }
        Ok(native_options)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bindings::load_library;

    #[test]
    fn options_left_out_keep_the_native_defaults() {
        let native_options = TaskbarContentHostOptions::default()
            .to_native(load_library().unwrap())
            .unwrap();

        assert_eq!(native_options.struct_size, 72);
        assert_eq!(native_options.preferred_width, 360.0);
        assert_eq!(native_options.preferred_height, 48.0);
        assert_eq!(
            native_options.layout_animation_easing,
            Easing::CircleOut as i32
        );
        assert_eq!(native_options.manual_slot_priority, 65535);
        assert_eq!(native_options.layout_refresh_interval, 500.0);
        assert_eq!(native_options.animate_layout_changes, 1);
    }

    #[test]
    fn options_that_are_set_replace_the_native_defaults() {
        let options = TaskbarContentHostOptions {
            preferred_width: Some(STRETCH),
            high_refresh_rate_mode: Some(false),
            manual_slot_priority: Some(10),
            layout_refresh_interval: Some(Duration::from_millis(250)),
            ..Default::default()
        };
        let native_options = options.to_native(load_library().unwrap()).unwrap();

        assert_eq!(native_options.preferred_width, f64::MAX);
        assert_eq!(native_options.high_refresh_rate_mode, 0);
        assert_eq!(native_options.manual_slot_priority, 10);
        assert_eq!(native_options.layout_refresh_interval, 250.0);
        assert_eq!(native_options.preferred_height, 48.0);
    }
}
