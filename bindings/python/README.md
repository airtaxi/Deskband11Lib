# deskband11lib for Python

[![PyPI](https://img.shields.io/pypi/v/deskband11lib.svg)](https://pypi.org/project/deskband11lib/)
[![Build and Publish Python Package](https://github.com/airtaxi/Deskband11Lib/actions/workflows/python-package.yml/badge.svg)](https://github.com/airtaxi/Deskband11Lib/actions/workflows/python-package.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/airtaxi/Deskband11Lib/blob/master/LICENSE)

deskband11lib places a Python window inside the Windows 11 taskbar. It finds the free space between the taskbar buttons and the notification area, moves the window there, and keeps it aligned when the taskbar layout changes, when the taskbar moves to another screen edge, and when other hosted windows share the same area.

The package wraps the Deskband11Lib native library through ctypes, so it works with any toolkit that can give you a window handle. A ready-made controller is included for PySide6 and PyQt6.

For how the library measures the taskbar, shares space between several hosted windows, and handles each taskbar position, see the [Deskband11Lib README](https://github.com/airtaxi/Deskband11Lib/blob/master/README.md). Deskband11Lib is also available for WinUI 3 and WPF as NuGet packages.

## Requirements

- Windows 11
- Python 3.10 or later, x64 or ARM64
- A per-monitor DPI aware window. Qt 6 is per-monitor DPI aware by default. For Tk and other toolkits, call `deskband11lib.enable_per_monitor_dpi_awareness()` before any window is created.

## Install

```powershell
pip install deskband11lib

# With PySide6
pip install "deskband11lib[pyside6]"
```

## Qt

`QtTaskbarWidgetController` creates your widget, removes its frame, attaches it to the taskbar, and shows it once it is in place.

```python
import sys

from PySide6.QtWidgets import QApplication, QLabel

from deskband11lib import TaskbarContentHostOptions
from deskband11lib.qt import QtTaskbarWidgetController

application = QApplication(sys.argv)
controller = QtTaskbarWidgetController(lambda: QLabel("Hello"), TaskbarContentHostOptions(preferred_width=120))
controller.start()
sys.exit(application.exec())
```

The taskbar destroys hosted windows when Explorer restarts or when the monitor they sit on is disconnected, and hosted windows are detached when the system DPI changes. The controller handles all three cases by closing the old widget and calling your factory again, so keep the widget state you want to preserve outside the widget. Because the widget is closed and created again, `start()` turns off `quitOnLastWindowClosed`; give users another way to exit, such as a context menu.

Connect to the controller hooks to react to the taskbar:

```python
@controller.taskbar_edge_changed.connect
def on_taskbar_edge_changed(event):
    controller.widget.apply_taskbar_orientation(event.taskbar_edge.is_vertical)
```

[`samples/pyside6_clock.py`](https://github.com/airtaxi/Deskband11Lib/blob/master/bindings/python/samples/pyside6_clock.py) shows a complete widget that follows the taskbar theme and switches its layout for vertical and thin taskbars.

## Any window

`TaskbarContentHost` takes a window handle (HWND) from any toolkit.

```python
from deskband11lib import TaskbarContentHost, TaskbarContentHostOptions

host = TaskbarContentHost(window_handle, TaskbarContentHostOptions(preferred_width=200))
host.taskbar_window_recreation_required.connect(on_recreation_required)
host.attach_when_layout_ready().add_done_callback(lambda future: show_window_without_activation())
```

Follow these rules when you host a window yourself:

- Create the host on the UI thread and call every method on that thread. The thread must run a Win32 message loop; the main loop of Qt, Tk, or wxPython does.
- Create the window hidden and without a frame. Show it without activating it after the host attaches.
- Leave the size and position of the window to the host while it is attached. The host applies them only when the taskbar layout changes, so a size set by the toolkit stays until the next layout change. Toolkits that fit a window to its content when it is first shown need the window resized to the size from the latest `content_bounds_changed` event before it is shown.
- When `taskbar_window_recreation_required` is raised, close the host, create the window again, and create a new host for it.
- Call `close()` when you no longer need the host. It restores the original parent and style of the window.

## Events

Each host exposes these hooks. Handlers receive a `HostEvent` and run on the UI thread from the message loop.

| Hook | Raised when |
| --- | --- |
| `attached` | `attach_when_layout_ready()` completed. |
| `attach_failed` | `attach_when_layout_ready()` failed. `error_message` describes why. |
| `taskbar_window_recreated` | Explorer restarted and created the taskbar again. |
| `taskbar_window_disappeared` | The taskbar the window sat on disappeared, for example because its monitor was disconnected. |
| `taskbar_dpi_changed` | The system DPI changed. The window has been hidden and detached. |
| `taskbar_window_recreation_required` | Raised together with each of the three events above. One handler covers all of them. |
| `taskbar_edge_changed` | The taskbar moved to another screen edge, including the first layout after attaching. |
| `content_bounds_changed` | The window received a new size, in effective pixels. |
| `error` | An unexpected error occurred while the layout was updated. |

## Options

| Option | Default | Description |
| --- | --- | --- |
| `preferred_width` | `360` | Length along the taskbar in effective pixels: the width on a bottom or top taskbar, the height on a left or right taskbar. `STRETCH` fills the space left by fixed-width hosts. |
| `preferred_height` | `48` | Size across the taskbar, capped by its thickness. |
| `placement` | `AUTO` | `AUTO`, `LEFT_EDGE`, `BEFORE_NOTIFICATION_AREA`, or `BEFORE_START_BUTTON`. |
| `preferred_monitor_identity` | `0` | `0` is the primary taskbar. `1` and above are secondary taskbars ordered from left to right. |
| `manual_slot_priority` | `65535` | Order among hosts that share an area. Lower values are placed first. `65535` orders by arrival. |
| `allow_fixed_slot_resize` | `True` | Lets fixed-width hosts shrink proportionally when space runs out. |
| `animate_layout_changes` | `True` | Animates position and size changes. |
| `high_refresh_rate_mode` | `True` | Matches the animation frame interval to the monitor refresh rate. |
| `layout_animation_duration` | `500` | Animation duration in milliseconds. |
| `layout_animation_easing` | `CIRCLE_OUT` | One of the `Easing` values. |
| `track_taskbar_buttons` | `True` | Measures the taskbar buttons through UI Automation so the window never covers them. |
| `track_notification_area` | `True` | Keeps the window away from the notification area. |
| `layout_refresh_interval` | `500` | Milliseconds between layout refreshes. |

Invalid values raise `Deskband11LibError` with `result` set to `ResultCode.INVALID_ARGUMENT` and a message that names the option.

## Building from source

Clone the [repository](https://github.com/airtaxi/Deskband11Lib). Building the native library requires the .NET 10 SDK and the Visual Studio C++ build tools.

```powershell
cd bindings/python

# Publish the native library for the running interpreter and stage it in the package
python scripts/stage_native.py

# Or stage several architectures to build one wheel that covers all of them
python scripts/stage_native.py win-x64 win-arm64

python -m build --wheel
```

Set `DESKBAND11LIB_LIBRARY_PATH` to load the native library from another location.

## Tests

```powershell
pip install -e ".[test,pyside6]"
pytest
```

Tests that attach windows to the taskbar of the current session are skipped unless `DESKBAND11LIB_TASKBAR_TESTS` is set to `1`. The Qt tests use PySide6; set `DESKBAND11LIB_QT_BINDING` to `PyQt6` to run them with PyQt6 instead.

## License

MIT
