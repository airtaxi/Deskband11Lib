# deskband11lib for Rust

[![crates.io](https://img.shields.io/crates/v/deskband11lib.svg)](https://crates.io/crates/deskband11lib)
[![docs.rs](https://img.shields.io/docsrs/deskband11lib)](https://docs.rs/deskband11lib)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/airtaxi/Deskband11Lib/blob/master/LICENSE)

deskband11lib places a native window inside the Windows 11 taskbar. It finds the free space between the taskbar buttons and the notification area, moves the window there, and keeps it aligned when the taskbar layout changes, when the taskbar moves to another screen edge, and when other hosted windows share the same area.

For Tauri applications, use [tauri-plugin-deskband11lib](https://crates.io/crates/tauri-plugin-deskband11lib), which creates, attaches and recreates webview windows for you. This crate is the lower-level host that works with any window handle.

For how the library measures the taskbar, shares space between several hosted windows, and handles each taskbar position, see the [Deskband11Lib README](https://github.com/airtaxi/Deskband11Lib/blob/master/README.md). Deskband11Lib is also available for WinUI 3 and WPF on NuGet, for Python on PyPI, and for Node.js and Electron on npm.

## Requirements

- Windows 11, x64 or ARM64
- Rust 1.90 or later

## Install

```powershell
cargo add deskband11lib
```

## Usage

```rust
use deskband11lib::{HostEvent, TaskbarContentHost, TaskbarContentHostOptions};

let options = TaskbarContentHostOptions { preferred_width: Some(200.0), ..Default::default() };
let host = TaskbarContentHost::new(window_handle, &options)?;
host.set_event_handler(|event| match event {
    HostEvent::Attached => show_window_without_activation(),
    HostEvent::TaskbarWindowRecreationRequired => recreate_window(),
    _ => {}
});
host.attach_when_layout_ready()?;
```

Follow these rules when you host a window:

- Create the host on a thread that runs a Win32 message loop and use it on that thread. `TaskbarContentHost` is not `Send`, so the compiler enforces this.
- The window must be per-monitor DPI aware. Create it hidden and without a frame.
- Show the window once while it is still a top-level window, then attach it. A webview that is shown for the first time after it became a child of the taskbar may display only its background.
- Leave the size and position of the window to the host while it is attached.
- When `HostEvent::TaskbarWindowRecreationRequired` is raised, close the host, create the window again, and create a new host for it. The taskbar destroys hosted windows when Explorer restarts or when their monitor is disconnected, and hosted windows are detached when the system DPI changes.
- Dropping the host, or calling `close()`, restores the original parent and style of the window. Both are safe inside the event handler.

## Events

| Event | Raised when |
| --- | --- |
| `Attached` | `attach_when_layout_ready()` completed. |
| `AttachFailed(error)` | `attach_when_layout_ready()` failed. |
| `TaskbarWindowRecreated` | Explorer restarted and created the taskbar again. |
| `TaskbarWindowDisappeared` | The taskbar the window sat on disappeared, for example because its monitor was disconnected. |
| `TaskbarDpiChanged` | The system DPI changed. The window has been hidden and detached. |
| `TaskbarWindowRecreationRequired` | Raised together with each of the three events above. One match arm covers all of them. |
| `TaskbarEdgeChanged(edge)` | The taskbar moved to another screen edge, including the first layout after attaching. |
| `ContentBoundsChanged { .. }` | The window received a new size, in effective pixels. |
| `LayoutError(error)` | An unexpected error occurred while the layout was updated. |

## Options

Options left as `None` keep the defaults of the native library.

| Option | Default | Description |
| --- | --- | --- |
| `preferred_width` | `360` | Length along the taskbar in effective pixels: the width on a bottom or top taskbar, the height on a left or right taskbar. `STRETCH` fills the space left by fixed-width hosts. |
| `preferred_height` | `48` | Size across the taskbar, capped by its thickness. |
| `placement` | `Auto` | `Auto`, `LeftEdge`, `BeforeNotificationArea`, or `BeforeStartButton`. |
| `preferred_monitor_identity` | `0` | `0` is the primary taskbar. `1` and above are secondary taskbars ordered from left to right. |
| `manual_slot_priority` | `65535` | Order among hosts that share an area. Lower values are placed first. `65535` orders by arrival. |
| `allow_fixed_slot_resize` | `true` | Lets fixed-width hosts shrink proportionally when space runs out. |
| `animate_layout_changes` | `true` | Animates position and size changes. |
| `high_refresh_rate_mode` | `true` | Matches the animation frame interval to the monitor refresh rate. |
| `layout_animation_duration` | `500 ms` | Animation duration. |
| `layout_animation_easing` | `CircleOut` | One of the `Easing` values. |
| `track_taskbar_buttons` | `true` | Measures the taskbar buttons through UI Automation so the window never covers them. |
| `track_notification_area` | `true` | Keeps the window away from the notification area. |
| `layout_refresh_interval` | `500 ms` | Interval between layout refreshes. |

Invalid values return `Error::Native` with `ResultCode::InvalidArgument` and a message that names the option.

## Native library

The crate bundles `deskband11lib.dll` for x64 and ARM64, and its build script copies the library for the target architecture next to the executables of the build. Ship that file next to your executable. At run time the library is loaded from the path in `DESKBAND11LIB_LIBRARY_PATH` when it is set, otherwise from the directory of the executable. Debug builds also fall back to the copy bundled with the crate.

## Building from source

Clone the [repository](https://github.com/airtaxi/Deskband11Lib). Building the native library requires the .NET 10 SDK and the Visual Studio C++ build tools.

```powershell
cd bindings/rust

# Publish the native library for this machine and stage it in the crate
./scripts/stage-native.ps1

# Or stage both architectures
./scripts/stage-native.ps1 -RuntimeIdentifiers win-x64,win-arm64

cargo test -p deskband11lib
```

Tests that attach a window to the taskbar of the current session run when `DESKBAND11LIB_TASKBAR_TESTS` is set to `1`.

## License

MIT
