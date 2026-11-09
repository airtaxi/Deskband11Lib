from __future__ import annotations

import ctypes
import sys
from dataclasses import dataclass

from ._bindings import NativeOptions, load_library
from .enums import Easing, TaskbarContentPlacement
from .errors import check_result

STRETCH = sys.float_info.max
"""A preferred_width that fills the space left after fixed-width hosts are served."""


@dataclass
class TaskbarContentHostOptions:
    """Options for a TaskbarContentHost. Sizes are in effective pixels and durations in milliseconds.

    preferred_width is the length along the taskbar and preferred_height the size across it, so on a left or
    right taskbar they map to the height and the width of the window.
    """

    preferred_width: float = 360.0
    """Length along the taskbar. Use STRETCH to fill the space left by fixed-width hosts."""

    preferred_height: float = 48.0
    """Size across the taskbar, capped by its thickness."""

    placement: TaskbarContentPlacement = TaskbarContentPlacement.AUTO
    preferred_monitor_identity: int = 0
    """0 is the primary taskbar. 1 and above are secondary taskbars ordered from left to right."""

    manual_slot_priority: int = 65535
    """0 to 65535. Lower values are placed first when several hosts share an area. 65535 orders by arrival."""

    allow_fixed_slot_resize: bool = True
    """Lets fixed-width hosts shrink proportionally when the available space runs out."""

    animate_layout_changes: bool = True
    high_refresh_rate_mode: bool = True
    """Matches the animation frame interval to the refresh rate of the monitor."""

    layout_animation_duration: float = 500.0
    layout_animation_easing: Easing = Easing.CIRCLE_OUT
    track_taskbar_buttons: bool = True
    """Measures the taskbar buttons through UI Automation so the window never covers them."""

    track_notification_area: bool = True
    layout_refresh_interval: float = 500.0

    def to_native(self) -> NativeOptions:
        if not 0 <= self.manual_slot_priority <= 65535:
            raise ValueError("manual_slot_priority must be between 0 and 65535.")

        native_options = NativeOptions()
        check_result(load_library().db11l_options_init(ctypes.byref(native_options), ctypes.sizeof(NativeOptions)))
        native_options.preferred_width = self.preferred_width
        native_options.preferred_height = self.preferred_height
        native_options.placement = int(self.placement)
        native_options.preferred_monitor_identity = self.preferred_monitor_identity
        native_options.manual_slot_priority = self.manual_slot_priority
        native_options.allow_fixed_slot_resize = int(self.allow_fixed_slot_resize)
        native_options.animate_layout_changes = int(self.animate_layout_changes)
        native_options.high_refresh_rate_mode = int(self.high_refresh_rate_mode)
        native_options.layout_animation_duration = self.layout_animation_duration
        native_options.layout_animation_easing = int(self.layout_animation_easing)
        native_options.track_taskbar_buttons = int(self.track_taskbar_buttons)
        native_options.track_notification_area = int(self.track_notification_area)
        native_options.layout_refresh_interval = self.layout_refresh_interval
        return native_options
