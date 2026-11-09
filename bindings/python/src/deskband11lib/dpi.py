from __future__ import annotations

import ctypes

_PER_MONITOR_AWARE_V2 = -4


def enable_per_monitor_dpi_awareness() -> bool:
    """Makes the process per-monitor DPI aware, which hosted windows require.

    Call this before any window is created. Qt 6 already does this. Returns False when the process DPI
    awareness was set earlier, for example by a UI toolkit or by the manifest of the executable.
    """
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    user32.SetProcessDpiAwarenessContext.argtypes = [ctypes.c_void_p]
    user32.SetProcessDpiAwarenessContext.restype = ctypes.c_int
    return user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(_PER_MONITOR_AWARE_V2)) != 0
