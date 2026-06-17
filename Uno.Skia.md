# Uno Skia Windows — HWND 및 Window API PoC

Uno Platform 6.5의 Skia Desktop 백엔드(Win32 호스트)에서 `Microsoft.UI.Xaml.Window`의 HWND를 획득하고, WinUI facade와 동일한 Window 조작 API를 사용할 수 있는지 검증한 PoC 기록입니다.

## 환경

| 항목 | 값 |
| --- | --- |
| .NET SDK | 10.0.301 |
| Uno SDK | Uno.Sdk 6.5.36 |
| Uno.WinUI | 6.5.237 (Uno.Sdk에 포함) |
| TFM | `net10.0-desktop` |
| 호스트 백엔드 | Skia Win32 (`UnoPlatformHostBuilder.Create().UseWin32()`) |
| 운영체제 | Windows 11 |

## 핵심 결론

Uno 6.5 Skia Desktop on Windows는 **WPF 호스트가 아닌 Win32 직접 호스트**를 사용합니다. 따라서 기존 WPF facade 패턴(`WindowInteropHelper`, `DispatcherTimer`)이 아니라 **WinUI facade 패턴**(`OverlappedPresenter`, `DispatcherQueue`)을 그대로 적용할 수 있습니다.

유일한 차이점은 HWND 획득 방법입니다.

## HWND 획득

### WinRT.Interop.WindowNative.GetWindowHandle — 사용 불가

Uno의 `WinRT.Interop.WindowNative.GetWindowHandle(window)`은 스텁 구현으로 항상 `1`을 반환합니다. 실제 HWND가 아닙니다.

```
WinRT.Interop.WindowNative.GetWindowHandle result: 1
```

### Uno.UI.Xaml.WindowHelper.GetNativeWindow — 공개 API (리플렉션 불필요)

Uno 공식 문서에 명시된 공개 정적 메서드입니다.

> `Uno.UI.Xaml.WindowHelper.GetNativeWindow(Window)` — `object?` 반환
>
> Skia+X11 → `Uno.UI.Runtime.Skia.X11NativeWindow`
> Skia+WPF → `System.Windows.Window`
> Skia+Win32 → `Uno.UI.NativeElementHosting.Win32NativeWindow`

Windows(Skia Win32)에서는 `Win32NativeWindow` 타입이 반환됩니다. 이 타입은 `public`이며, `Hwnd` 속성(`System.IntPtr`, `public`)으로 실제 Win32 HWND에 접근할 수 있습니다.

```csharp
using Uno.UI.NativeElementHosting;
using Uno.UI.Xaml;

var nativeWindow = WindowHelper.GetNativeWindow(window);
var win32Native = (Win32NativeWindow)nativeWindow;
nint hwnd = win32Native.Hwnd;  // 예: 0x2141A
```

PoC 실행 결과:

```
WindowHelper type: Uno.UI.Xaml.WindowHelper, Uno.UI, ...
GetNativeWindow method: GetNativeWindow
GetNativeWindow result: Uno.UI.NativeElementHosting.Win32NativeWindow
Hwnd property (public): System.IntPtr
Hwnd value: 1183864 (0x121078)
Type IsPublic: True
Direct cast to Win32NativeWindow: SUCCESS
Hwnd (direct): 136218 (0x2141A)
```

### 참고: 리플렉션 경로 (사용하지 않음)

리플렉션으로도 HWND에 접근할 수 있지만, 공개 API가 존재하므로 사용하지 않습니다.

- `Window.NativeWrapper` (internal) → `Win32WindowWrapper._hwnd` (internal field, `HWND` 타입)
- `Window.NativeWindow` (internal) → `Win32NativeWindow.Hwnd` (public property, `IntPtr` 타입)

## Window 조작 API 호환성

WinUI facade(`Deskband11Lib.WinUI`)와 동일한 API가 모두 정상 동작합니다.

| API | 상태 | 비고 |
| --- | --- | --- |
| `Window.AppWindow` | 정상 | `Microsoft.UI.Windowing.AppWindow` |
| `AppWindow.Presenter` (as `OverlappedPresenter`) | 정상 | `HasBorder`, `HasTitleBar`, `IsResizable`, `IsMaximizable`, `IsMinimizable` 모두 읽기/쓰기 가능 |
| `OverlappedPresenter.SetBorderAndTitleBar(bool, bool)` | 정상 | |
| `Window.ExtendsContentIntoTitleBar` | 정상 | `bool` 읽기/쓰기 |
| `Window.DispatcherQueue` | 정상 | `Microsoft.UI.Dispatching.DispatcherQueue` |
| `DispatcherQueue.TryEnqueue(Action)` | 정상 | |
| `DispatcherQueue.CreateTimer()` | 정상 | `DispatcherQueueTimer` |
| `FrameworkElement.Width` / `Height` / `MaxWidth` / `ActualWidth` / `ActualHeight` | 정상 | |
| `FrameworkElement.SizeChanged` | 정상 | |

PoC 실행 결과 (`OverlappedPresenter` 속성):

```
Presenter: Microsoft.UI.Windowing.OverlappedPresenter
  HasBorder = True
  HasTitleBar = True
  IsAlwaysOnTop = False
  IsMaximizable = True
  IsMinimizable = True
  IsResizable = True
  State = Restored
  Kind = Overlapped
```

## 로드된 어셈블리

WPF 어셈블리(`PresentationFramework`, `PresentationCore`, `WindowsBase`)는 로드되지 않습니다.

```
Uno.UI.Runtime.Skia.Win32
Uno.UI.Runtime.Skia.Win32.Support
Uno.UI.Runtime.Skia
```

## TFM 및 프로젝트 구성

| 항목 | 값 |
| --- | --- |
| SDK | `Uno.Sdk` (`global.json`의 `msbuild-sdks`에 명시) |
| TFM | `net10.0-desktop` |
| `UnoFeatures` | `SkiaRenderer` |
| `UseWinUI` | 설정하지 않음 (Uno.Sdk가 `Uno.WinUI`를 암시적으로 참조) |
| `UseWPF` | `false` (WPF 사용 안 함) |

### 주의사항

- `Uno.Sdk` 없이 `Microsoft.NET.Sdk` + `Uno.WinUI` 패키지 참조만으로는 빌드할 수 없습니다. `net10.0-windows10.0.26100.0` TFM에서 `win10-*` RID 인식 오류(`NETSDK1083`)가 발생합니다.
- `Uno.Sdk`가 RID 그래프와 Skia 런타임 패키지를 제공하므로 반드시 사용해야 합니다.
- `global.json`의 `msbuild-sdks` 섹션은 `Sdk="Uno.Sdk"`를 사용하는 프로젝트에만 영향을 주며, 기존 `Microsoft.NET.Sdk` 프로젝트에는 영향을 주지 않습니다.

## Core 프로젝트 참조 호환성

`Deskband11Lib.Core`는 `net10.0-windows10.0.26100.0`을 타겟합니다. `net10.0-desktop` 프로젝트에서 `net10.0-windows10.0.26100.0` 프로젝트를 참조할 수 있습니다. `net10.0-windows`는 `net10.0`의 플랫폼 특화 버전이므로 하위 호환성이 보장됩니다.

## Taskbar child-hosting 조사 메모

초기 PoC의 "WinUI facade와 거의 동일" 결론은 일반 Window API 조작에는 맞지만, **top-level Uno Skia Win32 window를 나중에 `Shell_TrayWnd`의 child window로 바꾸는 deskband hosting 시나리오에는 추가 제약이 있습니다.**

### App MCP 사용 조건

Uno App MCP/RemoteControl로 런타임 visual tree와 screenshot을 확인하려면 DEBUG에서 템플릿과 동일하게 `Window.UseStudio()`를 호출해야 합니다.

```csharp
#if DEBUG
window.UseStudio();
#endif
```

`UseStudio()`가 없으면 `Uno.UI.RemoteControl.Host`는 `5342` 포트를 열고 MCP endpoint도 응답하지만, `uno_app_get_runtime_info`, `uno_app_visualtree_snapshot`, `uno_app_get_screenshot` 같은 app tool 호출이 앱 응답을 기다리다가 timeout됩니다. 기본 템플릿 `E:\Repos\UnoApp1`은 `MainWindow.UseStudio()` 호출이 있어서 동일한 수동 HTTP MCP 호출로 정상 응답했습니다.

### 관측된 증상

`UseStudio()`를 추가한 뒤 `Deskband11Lib.Uno.Skia.Sample`을 실행하면 App MCP는 정상 동작합니다.

- `uno_app_get_runtime_info`가 `Window Title: Deskband11Lib.Uno.Skia.Sample`, 실제 sample PID를 정상 반환합니다.
- `uno_app_visualtree_snapshot`은 `SampleTaskbarContent`, `Border`, `Grid`, `FontIcon`, `Deskband11Lib` `TextBlock`, `Uno Skia Desktop sample` `TextBlock`, close `Button`을 모두 반환합니다.
- MCP visual tree 기준 root content bounds는 `266,108,424,48`로 관측됐습니다.
- Win32 기준 hosted HWND는 `UnoPlatformRegularWindow`, parent는 `Shell_TrayWnd`, rect는 `495,852,919,900 424x48`로 관측됐습니다.
- `Shell_TrayWnd` rect는 `0,828,1440,900`, `TrayNotifyWnd` rect는 `919,828,1440,900`이므로 Deskband layout 계산과 HWND placement 자체는 정상입니다.
- 같은 HWND 영역을 screen capture하면 taskbar 배경만 보입니다.
- 같은 HWND에 대한 `PrintWindow`도 성공값을 반환하지만 내용은 taskbar 배경/빈 표면입니다.

즉, **XAML visual tree는 살아 있고 content 크기도 계산되지만, 실제 OS taskbar 위치의 HWND에는 Uno Skia content가 present되지 않습니다.** 기존 Core layout, taskbar button 측정, `SetParent`, `SetWindowPos`, `SetWindowRgn`의 단순 실패라기보다는 Uno Skia Win32 renderer surface/viewport와 외부 Win32 reparenting 사이의 불일치로 보는 것이 더 타당합니다.

### 현재 가설

가장 유력한 가설은 Uno Skia Win32 renderer가 top-level window로 초기화될 때의 native window origin, client transform, swap chain/OpenGL surface, 혹은 clipping viewport를 내부에 들고 있고, 이후 외부에서 `SetParent`와 `SetWindowPos`로 HWND를 `Shell_TrayWnd` 아래로 옮겨도 renderer 쪽 좌표계/표면이 같은 방식으로 재초기화되지 않는다는 것입니다.

그 결과 OS 관점의 HWND는 taskbar의 `495,852`에 있지만, Uno 내부 visual tree는 기존 top-level window 좌표계처럼 `266,108` 기준으로 배치되며, 실제 taskbar child HWND client area에는 기대한 pixels가 나타나지 않습니다.

### Renderer reinitialize/internal API 조사 결과

`Uno.WinUI.Runtime.Skia.Win32` `6.5.237` 기준으로 renderer 재초기화와 관련된 내부 구현은 존재하지만 public API는 아닙니다.

- `Win32WindowWrapper`는 `internal`이고, `IRenderer`는 그 안의 `private interface`입니다.
- `Win32WindowWrapper`에는 `private readonly IRenderer _renderer`, `private SKSurface? _surface`, `private void ReinitializeRenderer()`가 있습니다.
- `ReinitializeRenderer()`는 `_renderer.Reinitialize()` 호출 후 `_surface`를 dispose하고 `null`로 되돌립니다.
- `GlRenderer.Reinitialize()`는 기존 GL context, `GRContext`, render target을 해제하고 같은 `_hdc`로 `wglCreateContext(_hdc)`와 `GRContext.CreateGl(_grGlInterface)`를 다시 호출합니다.
- `SoftwareRenderer.Reinitialize()`는 빈 구현입니다.
- Uno Win32 runtime source 기준으로 `ReinitializeRenderer()`는 정의만 있고 내부 호출처가 없습니다. 즉 attach 후 자동으로 타는 경로가 아니라, reflection 등으로 직접 호출해야만 실행됩니다.
- renderer 선택은 `Win32WindowWrapper` 생성자에서 `FeatureConfiguration.Rendering.UseOpenGLOnWin32 ?? true`로 결정됩니다. `false`면 `SoftwareRenderer`를 강제할 수 있습니다.
- `Win32Host.RenderSurfaceType` public property는 존재하지만, 현재 source 기준으로 `Win32WindowWrapper`의 renderer 선택에는 직접 연결되어 있지 않습니다.

따라서 1차 실험 후보는 두 가지입니다.

1. `SetParent`/style/size 적용 직후 `Win32WindowWrapper`를 reflection으로 찾아 `ReinitializeRenderer()`를 호출하고, 이어서 `InvalidateRect`/Uno render invalidation을 강제합니다.
2. 앱 시작 전에 `FeatureConfiguration.Rendering.UseOpenGLOnWin32 = false`를 설정해 software renderer로 taskbar child HWND present가 되는지 확인합니다.

첫 번째는 OpenGL context 재생성만 하고 기존 `_hdc`를 유지하므로 reparent 문제를 완전히 해결하지 못할 수 있습니다. 두 번째는 `BitBlt(GetDC(hwnd))` 경로라서 taskbar child HWND에는 더 잘 맞을 가능성이 있지만, 성능과 기본 지원 범위는 별도로 판단해야 합니다.

### 해결 방향 후보

1. **Uno adapter 전용 재초기화 경로 찾기**
   - `SetParent`/style 변경 직후 Uno Skia Win32 renderer에 size/location/surface 재초기화를 강제할 공개 또는 internal API가 있는지 확인합니다.
   - 패키지 문자열 기준으로는 `IRenderer.Reinitialize`, `ReinitializeRenderer`, `UpdateSize`, `RenderSurfaceType`, `UseOpenGLOnWin32`, `SoftwareRenderer` 같은 단서가 있습니다.

2. **reparent 전에 최종 parent/size를 가진 child HWND로 Uno host를 만들 수 있는지 확인**
   - 이미 top-level로 만들어진 HWND를 나중에 강제로 child로 바꾸는 방식이 renderer와 충돌한다면, native child-host 형태로 처음부터 만들 수 있는 API가 있는지 확인해야 합니다.
   - 없으면 public API만으로는 안정적인 solution이 아닐 수 있습니다.

3. **Software renderer fallback 검증**
   - OpenGL surface/swap chain이 parent 변경에 취약한 경우 software renderer로는 taskbar child HWND에 그려질 수 있는지 확인합니다.
   - 단, 이는 성능/지원 범위 tradeoff가 있고, library 기본값으로 삼기 전에 명시적 옵션으로 검증하는 편이 안전합니다.

4. **composition 대신 bitmap bridge 방식 검토**
   - Uno visual tree를 직접 taskbar child HWND에 live-present하지 못하면, Uno content를 offscreen/render-target bitmap으로 캡처하고 Core가 가진 별도 native child HWND에 blit하는 우회가 가능합니다.
   - 이 방식은 입력, focus, accessibility, animation, DPI, hit testing을 별도로 이어야 해서 마지막 수단에 가깝습니다.

5. **Uno Skia 지원 범위 명확화**
   - 일반 top-level sample과 App MCP/debug tooling은 정상 지원하고, taskbar child-hosting은 renderer 재초기화 방법이 확인될 때까지 experimental로 표시하는 선택지도 있습니다.

### Plan C: 처음부터 child HWND로 Uno host 생성 가능성

Uno 6.5.237 source link 기준으로는 **public API만으로 처음부터 child HWND인 Uno Window를 생성하는 경로는 없습니다.** 다만 Uno runtime을 patch/fork하거나 internal API를 건드리는 수준까지 허용하면 구조적으로는 가능합니다.

#### 확인한 생성 경로

- `UseWin32(Action<Win32HostBuilder>)`가 받는 공개 옵션은 현재 `PreloadMediaPlayer(bool)`뿐입니다. parent HWND, window style, initial rect, custom native window factory를 넘기는 옵션은 없습니다.
- `UseWindows(Action<IWindowsSkiaHostBuilder>)`도 WPF `Application` factory만 바꿀 수 있고, Uno가 실제로 만드는 `UnoWpfWindow` 생성 방식은 바꿀 수 없습니다.
- Skia desktop `Window`는 `DesktopWindow.Initialize()`에서 `DesktopWindowXamlSource`를 만들고, 이후 `BaseWindowImplementation.InitializeNativeWindow()`가 `NativeWindowFactory.CreateWindow(Window, XamlRoot)`를 호출합니다.
- Skia의 `NativeWindowFactory`는 `ApiExtensibility`에 등록된 internal `INativeWindowFactoryExtension`을 `Lazy`로 가져옵니다.
- Win32 backend의 기본 factory는 `Win32NativeWindowFactoryExtension.CreateWindow(...) => new Win32WindowWrapper(window, xamlRoot)`입니다.
- `Win32WindowWrapper.CreateWindow()`는 `CreateWindowEx(0, ..., WS_OVERLAPPEDWINDOW, ..., HWND.Null, ...)`로 고정되어 있습니다. 즉 parent가 `HWND.Null`이고 style이 top-level window용입니다.
- WPF backend의 기본 factory는 internal `UnoWpfWindow : System.Windows.Window`를 만들고, `WpfWindowWrapper`로 감쌉니다. WPF `Window` 기반이라 처음부터 taskbar child HWND로 만들 공개 경로는 없습니다. `WindowInteropHelper.Owner`는 owner 관계일 뿐 child parent가 아닙니다.

#### 의미

현재 방식은 `Window`/renderer가 top-level HWND 기준으로 생성된 뒤 `SetParent`와 style 변경으로 taskbar child가 됩니다. 지금 증상은 이 사후 reparenting과 renderer surface가 어긋나는 쪽이 유력합니다.

처음부터 child HWND로 만들려면 Win32 backend에서 `Win32WindowWrapper.CreateWindow()` 생성 인자를 바꿔야 합니다.

- style: `WS_OVERLAPPEDWINDOW` 대신 `WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN` 계열
- parent: `HWND.Null` 대신 `Shell_TrayWnd`
- initial rect: 최소 크기 또는 계산된 taskbar 영역
- activation/show: taskbar child window에 맞게 `ShowWindow`/`SetActiveWindow`/non-client 처리 일부 조정
- renderer: child HWND의 HDC로 OpenGL 또는 software renderer를 처음부터 초기화

이 경로라면 OpenGL HDC/viewport가 처음부터 taskbar child HWND에 묶이므로 현재의 blank-present 문제를 직접 겨냥합니다. 반대로 Uno public package 위에 얇게 얹는 라이브러리 구현으로는 범위를 벗어납니다.

#### 현실적인 구현 선택지

1. **Uno upstream PR 또는 runtime fork**
   - `Win32HostBuilder`에 window creation option/custom factory hook을 추가하고, Deskband11Lib는 그 hook을 통해 parent/style/rect를 넘깁니다.
   - 가장 깨끗하지만 범위가 큽니다.

2. **internal/reflection hack**
   - `ApiExtensibility` registry와 `NativeWindowFactory`의 lazy factory를 건드려 custom factory를 넣는 방향입니다.
   - 단, `INativeWindowFactoryExtension`, `INativeWindowWrapper`, `Win32WindowWrapper`가 모두 internal/private 구조라 동적 타입 생성이나 런타임 patch 수준이 필요합니다. 유지보수성이 낮습니다.

3. **현 패키지 유지 + 사후 reparenting 개선**
   - Plan A/B인 renderer reinitialize 또는 software renderer 강제가 여기에 해당합니다.
   - public API 위에서 끝낼 수 있는 가능성이 남아 있지만, child HWND from birth는 아닙니다.

따라서 Plan C는 "기술적으로 맞는 방향"이지만, Deskband11Lib.Uno.Skia의 일반 NuGet dependency 레이어에서 바로 구현할 수 있는 방식은 아닙니다. 이 방향으로 가려면 Uno 쪽에 공식 extension point를 추가하거나, 최소한 `Win32HostBuilder`/`Win32NativeWindowFactoryExtension`/`Win32WindowWrapper`에 child-hosting option이 들어가야 합니다.

### 다음 확인 순서

1. attach 전 일반 top-level 상태에서 App MCP screenshot/visual tree와 OS `PrintWindow`를 baseline으로 저장합니다.
2. attach 직후 같은 값들을 다시 저장합니다.
3. `SetParent`, `SetWindowLong`, `SetWindowPos`, `SetWindowRgn`, `ApplyContentBounds`, `Activate` 순서를 각각 바꿔도 visual tree bounds와 OS pixels가 어떻게 변하는지 비교합니다.
4. Uno Win32 runtime에서 renderer reinitialize 또는 software renderer 전환 방법을 찾습니다.
5. public API로 해결되지 않으면 Uno adapter는 지원 가능 범위를 낮추고, 별도 native child HWND + bitmap bridge가 필요한지 판단합니다.

## 결론

Uno Skia Windows facade는 일반적인 top-level Window API 조작 기준으로는 WinUI facade와 거의 동일한 구조로 구현할 수 있습니다. 유일한 차이점:

1. **HWND 획득**: `WinRT.Interop.WindowNative.GetWindowHandle(window)` 대신 `Uno.UI.Xaml.WindowHelper.GetNativeWindow(window)` → `(Win32NativeWindow).Hwnd` 사용
2. **TFM**: `net10.0-windows10.0.26100.0` 대신 `net10.0-desktop` (`Uno.Sdk` 필요)
3. **패키지 참조**: `Microsoft.WindowsAppSDK` 대신 `Uno.Sdk`가 `Uno.WinUI`를 암시적으로 참조
4. **`HAS_UNO` 심볼**: Uno.Sdk에 의해 자동으로 정의됨

나머지 모든 API(`OverlappedPresenter`, `DispatcherQueue`, `DispatcherQueueTimer`, `FrameworkElement` 속성/이벤트)는 WinUI facade와 동일하게 작동합니다.

다만 Deskband11Lib의 실제 taskbar hosting처럼 기존 Uno Skia Win32 top-level HWND를 `Shell_TrayWnd` child로 재부모화하는 시나리오는 현재 구현 보류가 맞습니다. Core layout과 HWND placement는 정상이나, Uno Skia renderer surface가 taskbar child HWND에 content를 present하지 못하므로 Uno 쪽에서 reparenting을 처리하지 못하는 버그로 보는 것이 타당합니다.

최종 결론은 다음과 같습니다.

- Uno runtime에서 reparenting 문제가 수정되기를 기다립니다.
- 또는 메이저 버전업마다 public API가 노출되거나, 유지 가능한 수준의 쉬운 reflection 경로가 생기는지 확인합니다.
- 그 전까지 `Deskband11Lib.Uno.Skia` 구현은 제품/패키지 범위에 넣지 않고 보류합니다.

현재 구현은 실험 기록과 재개용 코드로 별도 브랜치에 보관합니다.
