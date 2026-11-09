# deskband11lib for Node.js and Electron

[![npm](https://img.shields.io/npm/v/deskband11lib.svg)](https://www.npmjs.com/package/deskband11lib)
[![Build and Publish Node Package](https://github.com/airtaxi/Deskband11Lib/actions/workflows/node-package.yml/badge.svg)](https://github.com/airtaxi/Deskband11Lib/actions/workflows/node-package.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/airtaxi/Deskband11Lib/blob/master/LICENSE)

deskband11lib places an Electron window inside the Windows 11 taskbar. It finds the free space between the taskbar buttons and the notification area, moves the window there, and keeps it aligned when the taskbar layout changes, when the taskbar moves to another screen edge, and when other hosted windows share the same area.

The package calls the Deskband11Lib native library through koffi. It includes a controller for Electron and a lower-level host that works with any window handle.

For how the library measures the taskbar, shares space between several hosted windows, and handles each taskbar position, see the [Deskband11Lib README](https://github.com/airtaxi/Deskband11Lib/blob/master/README.md). Deskband11Lib is also available for WinUI 3 and WPF as NuGet packages, and for Python on PyPI.

## Requirements

- Windows 11, x64 or ARM64
- Node.js 22 or later. In Electron, use the package from the main process.

## Install

```powershell
npm install deskband11lib
```

## Electron

`ElectronTaskbarWindowController` creates your window, attaches it to the taskbar, and creates it again whenever the taskbar requires it.

```js
const { app, BrowserWindow } = require('electron');
const { ElectronTaskbarWindowController, createTaskbarWindowOptions } = require('deskband11lib/electron');

app.whenReady().then(() => {
    const controller = new ElectronTaskbarWindowController(() => {
        const window = new BrowserWindow(createTaskbarWindowOptions({ transparent: true, backgroundColor: '#00000000' }));
        window.loadFile('widget.html');
        return window;
    }, { preferredWidth: 200 });

    controller.start();
});
```

- `createTaskbarWindowOptions()` adds what a taskbar window needs: hidden until attached, no frame, no taskbar button or Alt+Tab entry, and no resizing or rounded corners. Use `transparent: true` to let the taskbar material show through the page.
- The taskbar layout decides the size of the window, so let the page fill the window. Do not show, move, or resize the window yourself.
- The taskbar follows the Windows mode, which can differ from the app mode the page sees through `prefers-color-scheme`. `nativeTheme.shouldUseDarkColorsForSystemIntegratedUI` tells you which one the taskbar uses.
- The taskbar destroys hosted windows when Explorer restarts or when the monitor they sit on is disconnected, and hosted windows are detached when the system DPI changes. The controller handles all three cases by destroying the old window and calling your factory again, so keep the state you want to preserve outside the window.
- While the controller runs, it keeps Electron from quitting when the last window closes. If your app quits from its own `window-all-closed` listener, skip that while `controller.isRecreating` is true, and give users another way to exit, such as a context menu.

Listen to the controller to react to the taskbar:

```js
const { isVerticalTaskbarEdge } = require('deskband11lib');

controller.on('taskbarEdgeChanged', event => {
    controller.window.webContents.send('taskbar-orientation', isVerticalTaskbarEdge(event.taskbarEdge));
});
```

[`samples/electron-clock`](https://github.com/airtaxi/Deskband11Lib/tree/master/bindings/node/samples/electron-clock) shows a complete widget that follows the taskbar theme and switches its layout for vertical and thin taskbars.

### Packaging

Windows cannot load a native library from inside an asar archive. Unpack the native files of deskband11lib and koffi; deskband11lib loads its library from `app.asar.unpacked` automatically.

- electron-builder unpacks koffi by itself. Add `"asarUnpack": ["**/node_modules/deskband11lib/bin/**"]`.
- Electron Forge: set `packagerConfig.asar` to `{ unpack: "**/*.{node,dll}" }`.

## Any window

`TaskbarContentHost` takes a native window handle (HWND), as a number, a bigint, or the buffer returned by `BrowserWindow.getNativeWindowHandle()`.

```js
const { TaskbarContentHost } = require('deskband11lib');

const host = new TaskbarContentHost(windowHandle, { preferredWidth: 200 });
host.on('taskbarWindowRecreationRequired', onRecreationRequired);
await host.attachWhenLayoutReady();
```

Follow these rules when you host a window yourself:

- Create the host on a thread that runs a Win32 message loop and call every method on that thread. The main process of Electron runs one; a plain Node.js process does not.
- The window must be per-monitor DPI aware. Electron windows are.
- Create the window hidden and without a frame. An Electron window must be shown once while it is still a top-level window, or it displays only its background color after it is attached; show it off-screen without activation, then attach it.
- When `taskbarWindowRecreationRequired` is emitted, close the host, create the window again, and create a new host for it.
- Call `close()` when you no longer need the host. It restores the original parent and style of the window.

Events are read from the native library every `eventPollInterval` milliseconds, 50 by default, and emitted on the same thread.

## Events

`TaskbarContentHost` emits these events. Each listener receives a `HostEvent`.

| Event | Emitted when |
| --- | --- |
| `attached` | `attachWhenLayoutReady()` completed. |
| `attachFailed` | `attachWhenLayoutReady()` failed. `errorMessage` describes why. |
| `taskbarWindowRecreated` | Explorer restarted and created the taskbar again. |
| `taskbarWindowDisappeared` | The taskbar the window sat on disappeared, for example because its monitor was disconnected. |
| `taskbarDpiChanged` | The system DPI changed. The window has been hidden and detached. |
| `taskbarWindowRecreationRequired` | Emitted together with each of the three events above. One listener covers all of them. |
| `taskbarEdgeChanged` | The taskbar moved to another screen edge, including the first layout after attaching. |
| `contentBoundsChanged` | The window received a new size, in effective pixels. |
| `layoutError` | An unexpected error occurred while the layout was updated. |

## Options

Options that are left out keep the defaults of the native library.

| Option | Default | Description |
| --- | --- | --- |
| `preferredWidth` | `360` | Length along the taskbar in effective pixels: the width on a bottom or top taskbar, the height on a left or right taskbar. `STRETCH` fills the space left by fixed-width hosts. |
| `preferredHeight` | `48` | Size across the taskbar, capped by its thickness. |
| `placement` | `Auto` | `Auto`, `LeftEdge`, `BeforeNotificationArea`, or `BeforeStartButton`. |
| `preferredMonitorIdentity` | `0` | `0` is the primary taskbar. `1` and above are secondary taskbars ordered from left to right. |
| `manualSlotPriority` | `65535` | Order among hosts that share an area. Lower values are placed first. `65535` orders by arrival. |
| `allowFixedSlotResize` | `true` | Lets fixed-width hosts shrink proportionally when space runs out. |
| `animateLayoutChanges` | `true` | Animates position and size changes. |
| `highRefreshRateMode` | `true` | Matches the animation frame interval to the monitor refresh rate. |
| `layoutAnimationDuration` | `500` | Animation duration in milliseconds. |
| `layoutAnimationEasing` | `CircleOut` | One of the `Easing` values. |
| `trackTaskbarButtons` | `true` | Measures the taskbar buttons through UI Automation so the window never covers them. |
| `trackNotificationArea` | `true` | Keeps the window away from the notification area. |
| `layoutRefreshInterval` | `500` | Milliseconds between layout refreshes. |
| `eventPollInterval` | `50` | Milliseconds between reads of host events. |

Invalid values throw `Deskband11LibError` with `result` set to `ResultCode.InvalidArgument` and a message that names the option.

## Building from source

Clone the [repository](https://github.com/airtaxi/Deskband11Lib). Building the native library requires the .NET 10 SDK and the Visual Studio C++ build tools.

```powershell
cd bindings/node
npm install

# Publish the native library for the running Node.js and stage it in the package
npm run stage-native

# Or stage several architectures
npm run stage-native -- x64 arm64

npm run build
npm test
```

`npm run test:taskbar` attaches windows to the taskbar of the current session, and `npm run sample` runs the clock sample. Set `DESKBAND11LIB_LIBRARY_PATH` to load the native library from another location.

## License

MIT
