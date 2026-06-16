# Deskband11Lib

[![NuGet](https://img.shields.io/nuget/v/Deskband11Lib.svg)](https://www.nuget.org/packages/Deskband11Lib)
[![NuGet downloads](https://img.shields.io/nuget/dt/Deskband11Lib.svg)](https://www.nuget.org/packages/Deskband11Lib)
[![Pack and Publish](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml/badge.svg)](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

🌐 English | [한국어](README.ko.md)

Deskband11Lib is a WinUI 3 library for building rich, always-visible taskbar companions on Windows 11. It lets your app place real WinUI content directly inside the taskbar, making compact dashboards, quick controls, status indicators, media widgets, launchers, and productivity tools feel like a native part of the desktop.

![Deskband11Lib screenshot](.github/screenshot.png)

## Highlights

- Build taskbar-resident widgets with the same WinUI 3 controls, styling, and composition features used by your app.
- Show live information where users can see it at a glance without opening a full window.
- Add compact controls for actions such as timers, media playback, account switching, build status, device monitoring, or quick launch workflows.
- Automatically fit content into the available taskbar space without overlapping pinned apps or the notification area.
- Recover cleanly when Explorer restarts by letting the application rebuild its hosted window.
- Support Windows App SDK apps and NativeAOT publishing.

## Install

```powershell
dotnet add package Deskband11Lib
```

## Basic Usage

Create a WinUI window, create a `TaskbarContentHost`, attach it after the initial taskbar layout is ready, then activate the window.

```csharp
var window = new MainWindow();
var host = new TaskbarContentHost(window, rootElement, new TaskbarContentHostOptions
{
    PreferredWidth = 360,
    PreferredHeight = 48
});

await host.AttachWhenLayoutReadyAsync();
window.Activate();
```

When Explorer restarts, the taskbar destroys the hosted child window. The safest recovery strategy is to let the application create a new WinUI window and attach that new window to the recreated taskbar.

```csharp
host.TaskbarWindowRecreated += async (_, _) =>
{
    await RecreateMainWindowAsync();
};
```

## How It Works

Deskband11Lib gives your WinUI app a taskbar-sized surface and keeps that surface aligned with the real taskbar layout. Internally, it uses regular Win32 window parenting:

- Finds the primary taskbar window, `Shell_TrayWnd`.
- Creates or receives a normal WinUI `Window` from the application.
- Changes the WinUI window style from popup-style top-level window to child window.
- Calls `SetParent` to place the WinUI window under the taskbar.
- Calculates the available rectangle between the taskbar buttons and the notification area.
- Moves and clips the hosted window to that rectangle with `SetWindowPos` and `SetWindowRgn`.

Taskbar button width is not reliable from the taskbar child HWND hierarchy alone on current Windows 11 builds. Deskband11Lib therefore uses UI Automation to inspect the taskbar's visible button rectangles. The UI Automation scan runs off the UI thread and is cached so layout refreshes do not block the hosted WinUI content.

## Explorer Restart Handling

Explorer owns the taskbar. When Explorer crashes or restarts, the old taskbar window and any child windows under it are destroyed. Reusing the old WinUI `Window` after that point can be unstable, so Deskband11Lib reports taskbar recreation instead of automatically reattaching the old window.

Applications should handle `TaskbarWindowRecreated`, release the old host/window, create a fresh window, and call `AttachWhenLayoutReadyAsync()` again.

## Options

- `PreferredWidth`: Desired content width in effective pixels.
- `PreferredHeight`: Desired content height in effective pixels.
- `StartAreaWidth`: Reserved width for the Start button area.
- `Placement`: Places content before the notification area or after taskbar buttons.
- `TrackTaskbarButtons`: Enables UI Automation based taskbar button measurement.
- `TrackNotificationArea`: Keeps content away from the notification area.
- `LayoutRefreshInterval`: Refresh interval for ongoing taskbar layout updates.

## Requirements

- Windows 11.
- WinUI 3 / Windows App SDK.
- A Windows target framework compatible with Windows App SDK.

The sample project targets `net10.0-windows10.0.26100.0` and supports NativeAOT publishing.

## Project Development

```powershell
dotnet restore
dotnet build Deskband11Lib.slnx -c Debug
dotnet publish Deskband11Lib.Sample\Deskband11Lib.Sample.csproj -c Release -r win-x64
```

## Acknowledgements

Special thanks to [zadjii](https://github.com/zadjii) and [Deskband11](https://github.com/zadjii/Deskband11). The core idea of bringing application content into the Windows 11 taskbar comes from that project, and Deskband11Lib is grateful for the brilliant inspiration.

## License

Deskband11Lib is licensed under the [MIT License](LICENSE).

## Author

Created by [Howon Lee (airtaxi)](https://github.com/airtaxi).
