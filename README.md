# Deskband11Lib

[![NuGet WinUI](https://img.shields.io/nuget/v/Deskband11Lib.WinUI.svg)](https://www.nuget.org/packages/Deskband11Lib.WinUI)
[![NuGet WPF](https://img.shields.io/nuget/v/Deskband11Lib.Wpf.svg)](https://www.nuget.org/packages/Deskband11Lib.Wpf)
[![Pack and Publish](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml/badge.svg)](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

🌐 English | [한국어](README.ko.md)

Deskband11Lib is a library for building rich, always-visible taskbar companions on Windows 11. It lets your app place real UI content directly inside the taskbar, making compact dashboards, quick controls, status indicators, media widgets, launchers, and productivity tools feel like a native part of the desktop.

![Deskband11Lib screenshot](.github/screenshot.png)

## Packages

Deskband11Lib comes in multiple NuGet packages, one for each supported UI framework. A shared `Deskband11Lib.Core` package holds the taskbar hosting engine. Future frameworks such as Avalonia can be added as additional facade packages.

| Package               | Description                                                                                                                                                           |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Deskband11Lib.Core`  | Taskbar window discovery, layout calculation, UI Automation measurement, Explorer restart monitoring, and Win32 HWND hosting engine. Independent of any UI framework. |
| `Deskband11Lib.WinUI` | WinUI 3 facade. Build taskbar widgets with the same WinUI controls, styling, and composition features used by your app.                                               |
| `Deskband11Lib.Wpf`   | WPF facade. Bring WPF-based content into the Windows 11 taskbar with the same simple API.                                                                             |

## Highlights

- Build taskbar-resident widgets using your framework's native controls and styling.
- Show live information where users can see it at a glance without opening a full window.
- Add compact controls for actions such as timers, media playback, account switching, build status, device monitoring, or quick launch workflows.
- Automatically fit content into the available taskbar space without overlapping pinned apps or the notification area.
- Built-in easing functions (Linear, Sine, Quadratic, Cubic, Quartic, Quintic, Exponential, Circle) for smooth layout animations.
- Optional high refresh rate mode that matches the layout animation timer to the target monitor's current refresh rate for smoother motion on high refresh rate displays.
- Recover cleanly when Explorer restarts by letting the application rebuild its hosted window.
- WinUI facade supports Windows App SDK apps and NativeAOT publishing.

## Install

Choose the package that matches your UI framework:

```powershell
# WinUI 3
dotnet add package Deskband11Lib.WinUI

# WPF
dotnet add package Deskband11Lib.Wpf
```

The `Deskband11Lib.Core` package is pulled in automatically as a transitive dependency.

## Basic Usage

### WinUI 3

Create a WinUI window, create a `TaskbarContentHost`, attach it after the initial taskbar layout is ready, then activate the window.

```csharp
using Deskband11Lib.Core;
using Deskband11Lib.WinUI;

var window = new MainWindow();
var host = new TaskbarContentHost(window, rootElement, new TaskbarContentHostOptions
{
    PreferredWidth = 360,
    PreferredHeight = 48
});

await host.AttachWhenLayoutReadyAsync();
window.Activate();
```

### WPF

The WPF API is identical. The only difference is the UI framework namespace used for `Window` and `FrameworkElement`.

```csharp
using Deskband11Lib.Core;
using Deskband11Lib.Wpf;

var window = new MainWindow();
var host = new TaskbarContentHost(window, rootElement, new TaskbarContentHostOptions
{
    PreferredWidth = 360,
    PreferredHeight = 48
});

await host.AttachWhenLayoutReadyAsync();
window.Show();
```

### Explorer Restart

When Explorer restarts, the taskbar destroys the hosted child window. Handle `TaskbarWindowRecreated` to replace the window:

```csharp
host.TaskbarWindowRecreated += async (_, _) =>
{
    await RecreateMainWindowAsync();
};
```

### Secondary Monitor Reconnection

When you set `PreferredMonitorIdentity` to a secondary monitor's taskbar and that monitor is later disconnected, Deskband11Lib automatically falls back to the primary taskbar. When the secondary monitor is reconnected, the hosted window moves back to the secondary monitor's taskbar automatically — no application code is required.

## How It Works

Deskband11Lib gives your app a taskbar-sized surface and keeps that surface aligned with the real taskbar layout. Internally, it uses regular Win32 window parenting:

- Finds the primary taskbar window, `Shell_TrayWnd`, or a secondary monitor's taskbar window, `Shell_SecondaryTrayWnd`, based on `PreferredMonitorIdentity`.
- Creates or receives a normal framework `Window` from the application.
- Changes the window style from popup-style top-level window to child window.
- Calls `SetParent` to place the window under the taskbar.
- Calculates the available rectangle between the taskbar buttons and the notification area, taking taskbar alignment (left or center) into account.
- Moves and clips the hosted window to that rectangle with `SetWindowPos` and `SetWindowRgn`.

On current Windows 11 builds, the taskbar child HWND hierarchy alone does not expose reliable button widths, and the taskbar content may be left-aligned or centered. Deskband11Lib solves this with UI Automation:

- **Button detection.** UI Automation precisely targets the Start button (`AutomationId = StartButton`), the optional Widgets button, and the contiguous taskbar app button group.
- **Alignment detection.** The library reads the `TaskbarAl` registry value and falls back to inferring alignment from the Start button position.
- **Gap selection.** When centered, it picks the more spacious of the two free gaps around the Start button group, so content never overlaps the Start button, app buttons, Widgets button, or the notification area.
- **Secondary monitors.** Where the notification area has no dedicated HWND, UI Automation also locates the clock and notification cluster (`AutomationId = SystemTrayIcon` / `NotifyItemIcon`) to compute the right boundary.

## Options

All options live in `Deskband11Lib.Core.TaskbarContentHostOptions` and are shared across all facades.

| Option                    | Default                     | Description                                                                                                                                              |
| ------------------------- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PreferredWidth`          | `360`                       | Desired content width in effective pixels.                                                                                                               |
| `PreferredHeight`         | `48`                        | Desired content height in effective pixels.                                                                                                              |
| `AnimateLayoutChanges`    | `true`                      | Animates taskbar host position and size changes.                                                                                                         |
| `HighRefreshRateMode`     | `false`                     | When enabled along with `AnimateLayoutChanges`, matches the layout animation timer interval to the target monitor's current refresh rate instead of the default 60 FPS. |
| `LayoutAnimationDuration` | `500`                       | Layout animation duration in milliseconds.                                                                                                               |
| `LayoutAnimationEasing`   | `EasingFunctions.CircleOut` | Easing delegate (`Func<double, double>`) for layout animation. Built-in non-overshooting functions are provided by `Deskband11Lib.Core.EasingFunctions`. |
| `Placement`               | `Auto`                      | Selects placement.<br>• `Auto` — When centered, picks the more spacious free gap around the Start button group. Left gap wins → far-left edge (same as `LeftEdge`); right gap wins → before the notification area. Falls back to `BeforeNotificationArea` when left-aligned.<br>• `LeftEdge` — When centered, places content at the far left edge, right after the Widgets button. Falls back to `BeforeNotificationArea` when left-aligned.<br>• `BeforeNotificationArea` — Always places content next to the notification area.<br>• `BeforeStartButton` — Places content next to the Start button. Falls back to `BeforeNotificationArea` when not centered. |
| `TrackTaskbarButtons`     | `true`                      | Enables UI Automation based taskbar button measurement.                                                                                                  |
| `TrackNotificationArea`   | `true`                      | Keeps content away from the notification area.                                                                                                           |
| `PreferredMonitorIdentity` | `0`                       | Selects which taskbar to host on. `0` uses the primary taskbar (`Shell_TrayWnd`). `1` uses the first secondary monitor's taskbar (`Shell_SecondaryTrayWnd`), `2` the next, and so on, ordered left-to-right by screen position. Falls back to the primary taskbar when the requested secondary monitor's taskbar is not present. |
| `LayoutRefreshInterval`   | `500 ms`                    | Refresh interval for ongoing taskbar layout updates.                                                                                                     |

## Taskbar Alignment

Deskband11Lib detects whether the Windows 11 taskbar is left-aligned or centered by reading the `TaskbarAl` registry value, falling back to inferring the alignment from the Start button position. Call `GetTaskbarAlignment()` on a `TaskbarContentHost` to get a `TaskbarAlignment` (`Left`, `Center`, or `Unknown`). The detected alignment drives `Placement = Auto`.

## Built-in Easing Functions

`Deskband11Lib.Core.EasingFunctions` provides these easing functions for `LayoutAnimationEasing`:

- `EasingFunctions.Linear`
- `EasingFunctions.SineIn` / `SineOut` / `SineInOut`
- `EasingFunctions.QuadraticIn` / `QuadraticOut` / `QuadraticInOut`
- `EasingFunctions.CubicIn` / `CubicOut` / `CubicInOut`
- `EasingFunctions.QuarticIn` / `QuarticOut` / `QuarticInOut`
- `EasingFunctions.QuinticIn` / `QuinticOut` / `QuinticInOut`
- `EasingFunctions.ExponentialIn` / `ExponentialOut` / `ExponentialInOut`
- `EasingFunctions.CircleIn` / `CircleOut` / `CircleInOut`

You can also pass any `Func<double, double>` delegate for custom easing.

## Sample Projects

The snippets above show the core API shape, but a real taskbar companion should follow the sample projects for window lifetime, startup ordering, Explorer restart recovery, and framework-specific hosting details. Start from the sample that matches your UI stack:

- `Deskband11Lib.WinUI.Sample` for WinUI 3 and Windows App SDK apps.
- `Deskband11Lib.Wpf.Sample` for WPF apps, including the transparent borderless host window setup.

## Requirements

- Windows 11.
- The target framework must be compatible with your chosen UI framework.
- WinUI 3 requires Windows App SDK.
- WPF requires `UseWPF=true` in the project file.

## Project Development

```powershell
dotnet restore
dotnet build Deskband11Lib.slnx -c Debug
dotnet publish Deskband11Lib.WinUI.Sample\Deskband11Lib.WinUI.Sample.csproj -c Release -r win-x64
```

## Acknowledgements

Special thanks to [zadjii](https://github.com/zadjii) and [Deskband11](https://github.com/zadjii/Deskband11). The core idea of bringing application content into the Windows 11 taskbar comes from that project, and Deskband11Lib is grateful for the brilliant inspiration.

## License

Deskband11Lib is licensed under the [MIT License](LICENSE).

## Author

Created by [Howon Lee (airtaxi)](https://github.com/airtaxi).
