# tauri-plugin-deskband11lib

[![crates.io](https://img.shields.io/crates/v/tauri-plugin-deskband11lib.svg)](https://crates.io/crates/tauri-plugin-deskband11lib)
[![docs.rs](https://img.shields.io/docsrs/tauri-plugin-deskband11lib)](https://docs.rs/tauri-plugin-deskband11lib)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/airtaxi/Deskband11Lib/blob/master/LICENSE)

A Tauri v2 plugin that places a webview window inside the Windows 11 taskbar. The window sits in the free space between the taskbar buttons and the notification area, and stays aligned when the taskbar layout changes, when the taskbar moves to another screen edge, and when other hosted windows share the same area.

For how the library measures the taskbar, shares space between several hosted windows, and handles each taskbar position, see the [Deskband11Lib README](https://github.com/airtaxi/Deskband11Lib/blob/master/README.md). The plugin builds on the [deskband11lib](https://crates.io/crates/deskband11lib) crate.

## Requirements

- Windows 11, x64 or ARM64
- Tauri 2.12 or later and Rust 1.90 or later

The plugin is Windows-only. In a cross-platform application, add it as a Windows dependency and register it on Windows only.

## Install

In `src-tauri`:

```powershell
cargo add tauri-plugin-deskband11lib --target "cfg(windows)"
```

## Usage

Register the plugin and start a controller from the setup hook:

```rust
use tauri::WebviewUrl;
use tauri_plugin_deskband11lib::{TaskbarContentHostOptions, TaskbarWindowController, taskbar_window_builder};

tauri::Builder::default()
    .plugin(tauri_plugin_deskband11lib::init())
    .setup(|app| {
        TaskbarWindowController::new(app.handle(), |app| {
            taskbar_window_builder(app, "widget", WebviewUrl::App("index.html".into()))
                .transparent(true)
                .build()
        })
        .options(TaskbarContentHostOptions { preferred_width: Some(200.0), ..Default::default() })
        .start()?;
        Ok(())
    })
    .run(tauri::generate_context!())
    .expect("The application failed to run.");
```

- `taskbar_window_builder()` returns a window builder with what a taskbar window needs: hidden until attached, not focused, and no decorations, taskbar button, resizing or shadow. Add your own settings, such as `transparent(true)`, and build it.
- The controller shows the window off-screen, attaches it to the taskbar, and calls your factory again whenever the taskbar requires it. The taskbar destroys hosted windows when Explorer restarts or when their monitor is disconnected, and hosted windows are detached when the system DPI changes. Keep the state you want to preserve outside the window.
- The taskbar layout decides the size of the window, so let the page fill it. Do not show, move or resize the window yourself.
- While a controller runs, the plugin keeps Tauri from exiting when its last window closes, because the taskbar destroys the window before it is created again. Exit with `AppHandle::exit`, for example from a context menu.
- The taskbar follows the Windows mode, which can differ from the app mode the page sees through `prefers-color-scheme`. Read `SystemUsesLightTheme` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` to match it.

`start()` returns a `TaskbarWindowHandle` with the current `window()`, `recreate()` to rebuild the window, and `stop()`. Use `on_event()` on the controller to receive every host event in Rust.

### Frontend events

When the taskbar moves to another screen edge, including the first layout after attaching, the plugin emits `deskband11lib://taskbar-edge-changed` to the hosted window. The payload is `{ taskbarEdge: "left" | "top" | "right" | "bottom", isVertical: boolean }`. Listening needs the `core:event:default` permission, which `core:default` includes.

```js
import { listen } from '@tauri-apps/api/event';

await listen('deskband11lib://taskbar-edge-changed', event => {
    document.body.classList.toggle('vertical', event.payload.isVertical);
});
```

### Bundling

The build copies `deskband11lib.dll` next to the executable, which covers `tauri dev`. Add it to the bundle in `tauri.conf.json` so that the installer places it next to the executable as well:

```json
{
  "bundle": {
    "resources": {
      "target/release/deskband11lib.dll": "./"
    }
  }
}
```

The source path is relative to `src-tauri`. Adjust it when the project uses another target directory, such as a Cargo workspace, or builds for an explicit `--target`.

## Sample

[`samples/tauri-clock`](https://github.com/airtaxi/Deskband11Lib/tree/master/bindings/rust/samples/tauri-clock) is a clock that follows the taskbar theme, switches its layout for vertical and thin taskbars, and exits from a native context menu. Run it from `bindings/rust` with `cargo run -p tauri-clock`.

## License

MIT
