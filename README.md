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

## How It Works

Deskband11Lib gives your app a taskbar-sized surface and keeps that surface aligned with the real taskbar layout. Internally, it uses regular Win32 window parenting:

- Finds the primary taskbar window, `Shell_TrayWnd`.
- Creates or receives a normal framework `Window` from the application.
- Changes the window style from popup-style top-level window to child window.
- Calls `SetParent` to place the window under the taskbar.
- Calculates the available rectangle between the taskbar buttons and the notification area.
- Moves and clips the hosted window to that rectangle with `SetWindowPos` and `SetWindowRgn`.

Taskbar button width is not reliable from the taskbar child HWND hierarchy alone on current Windows 11 builds. Deskband11Lib therefore uses UI Automation to inspect the taskbar's visible button rectangles. The UI Automation scan runs off the UI thread and is cached so layout refreshes do not block the hosted content.

## Options

All options live in `Deskband11Lib.Core.TaskbarContentHostOptions` and are shared across all facades.

| Option                    | Default                     | Description                                                                                                                                              |
| ------------------------- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PreferredWidth`          | `360`                       | Desired content width in effective pixels.                                                                                                               |
| `PreferredHeight`         | `48`                        | Desired content height in effective pixels.                                                                                                              |
| `AnimateLayoutChanges`    | `true`                      | Animates taskbar host position and size changes.                                                                                                         |
| `LayoutAnimationDuration` | `500`                       | Layout animation duration in milliseconds.                                                                                                               |
| `LayoutAnimationEasing`   | `EasingFunctions.CircleOut` | Easing delegate (`Func<double, double>`) for layout animation. Built-in non-overshooting functions are provided by `Deskband11Lib.Core.EasingFunctions`. |
| `StartAreaWidth`          | `60`                        | Reserved width for the Start button area.                                                                                                                |
| `Placement`               | `BeforeNotificationArea`    | Places content before the notification area or after taskbar buttons.                                                                                    |
| `TrackTaskbarButtons`     | `true`                      | Enables UI Automation based taskbar button measurement.                                                                                                  |
| `TrackNotificationArea`   | `true`                      | Keeps content away from the notification area.                                                                                                           |
| `LayoutRefreshInterval`   | `500 ms`                    | Refresh interval for ongoing taskbar layout updates.                                                                                                     |

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
