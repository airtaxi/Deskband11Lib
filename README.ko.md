# Deskband11Lib

[![NuGet WinUI](https://img.shields.io/nuget/v/Deskband11Lib.WinUI.svg)](https://www.nuget.org/packages/Deskband11Lib.WinUI)
[![NuGet WPF](https://img.shields.io/nuget/v/Deskband11Lib.Wpf.svg)](https://www.nuget.org/packages/Deskband11Lib.Wpf)
[![Pack and Publish](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml/badge.svg)](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

🌐 [English](README.md) | 한국어

Deskband11Lib는 Windows 11 작업 표시줄 안에 앱 UI를 자연스럽게 올릴 수 있게 해주는 라이브러리입니다. 작은 대시보드, 빠른 제어 패널, 상태 표시기, 미디어 위젯, 런처처럼 항상 눈에 띄어야 하는 기능을 데스크톱의 일부처럼 만들 수 있습니다.

![Deskband11Lib 스크린샷](.github/screenshot.png)

## 패키지 구성

Deskband11Lib는 UI 프레임워크별 NuGet 패키지로 제공됩니다. 작업 표시줄에 붙이는 핵심 로직은 `Deskband11Lib.Core`에 들어 있고, WinUI와 WPF 패키지는 각 프레임워크에서 쓰기 쉬운 API를 제공합니다. 이후 Avalonia 같은 프레임워크도 같은 방식으로 추가할 수 있습니다.

| 패키지                   | 설명                                                                                                   |
| --------------------- | ---------------------------------------------------------------------------------------------------- |
| `Deskband11Lib.Core`  | 작업 표시줄 창 찾기, 레이아웃 계산, UI Automation 측정, Explorer 재시작 감지, Win32 HWND 호스팅을 담당합니다. UI 프레임워크에 의존하지 않습니다. |
| `Deskband11Lib.WinUI` | WinUI 3 앱용 패키지입니다. 앱에서 쓰던 WinUI 컨트롤, 스타일, Composition 기능으로 작업 표시줄 위젯을 만들 수 있습니다.                     |
| `Deskband11Lib.Wpf`   | WPF 앱용 패키지입니다. WPF로 만든 콘텐츠를 같은 방식으로 Windows 11 작업 표시줄에 배치할 수 있습니다.                                   |

## 주요 기능

- 앱에서 쓰는 컨트롤과 스타일을 그대로 활용해 작업 표시줄 위젯을 만들 수 있습니다.
- 별도 창을 열지 않아도 필요한 정보를 항상 보이는 위치에 띄울 수 있습니다.
- 타이머, 미디어 재생, 계정 전환, 빌드 상태, 장치 모니터링, 빠른 실행 같은 기능을 작게 정리해 배치하기 좋습니다.
- 고정 앱, 실행 중인 앱, 알림 영역과 겹치지 않도록 남는 작업 표시줄 공간에 맞춰 위치를 잡습니다.
- 레이아웃이 바뀔 때 자연스럽게 움직이도록 기본 easing 함수를 제공합니다. Linear, Sine, Quadratic, Cubic, Quartic, Quintic, Exponential, Circle 계열을 사용할 수 있습니다.
- Explorer가 재시작되면 앱이 작업 표시줄 창을 다시 붙일 수 있도록 이벤트를 제공합니다.
- WinUI 패키지는 Windows App SDK 앱과 NativeAOT 게시를 지원합니다.

## 설치

사용 중인 UI 프레임워크에 맞는 패키지를 설치하시면 됩니다.

```powershell
# WinUI 3
dotnet add package Deskband11Lib.WinUI

# WPF
dotnet add package Deskband11Lib.Wpf
```

`Deskband11Lib.Core`는 필요한 경우 자동으로 함께 설치됩니다.

## 기본 사용법

### WinUI 3

WinUI 창을 만든 뒤 `TaskbarContentHost`에 연결합니다. 초기 작업 표시줄 레이아웃이 준비되면 창을 작업 표시줄에 붙이고 활성화합니다.

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

WPF에서도 API 흐름은 같습니다. 차이는 `Window`와 `FrameworkElement`가 WPF 타입이라는 점뿐입니다.

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

### Explorer 재시작

Explorer가 재시작되면 작업 표시줄에 붙어 있던 자식 창도 사라집니다. 이때는 `TaskbarWindowRecreated` 이벤트에서 창을 다시 만들어 붙이면 됩니다.

```csharp
host.TaskbarWindowRecreated += async (_, _) =>
{
    await RecreateMainWindowAsync();
};
```

## 동작 방식

Deskband11Lib는 앱 창을 작업 표시줄 안에 들어갈 크기로 맞추고, 실제 작업 표시줄 레이아웃이 바뀔 때마다 위치를 다시 잡습니다. 내부적으로는 일반적인 Win32 창 부모 관계를 사용합니다.

- 기본 작업 표시줄 창인 `Shell_TrayWnd`를 찾습니다.
- 앱에서 만든 일반 `Window`를 받습니다.
- 창 스타일을 최상위 팝업 창에서 자식 창 스타일로 바꿉니다.
- `SetParent`로 창을 작업 표시줄의 자식 창으로 붙입니다.
- 작업 표시줄 버튼과 알림 영역 사이에서 사용할 수 있는 영역을 계산합니다.
- `SetWindowPos`와 `SetWindowRgn`으로 창 위치와 클리핑 영역을 조정합니다.

현재 Windows 11에서는 작업 표시줄의 자식 HWND만 살펴봐서는 실제 버튼 폭을 안정적으로 알기 어렵습니다. 그래서 Deskband11Lib는 UI Automation으로 화면에 보이는 작업 표시줄 버튼들의 위치를 확인합니다. 이 작업은 UI 스레드 밖에서 실행되고 결과도 캐시되므로, 레이아웃을 갱신하는 동안 앱 UI가 멈추지 않습니다.

## 옵션

모든 옵션은 `Deskband11Lib.Core.TaskbarContentHostOptions`에 정의되어 있고, WinUI와 WPF 패키지에서 공통으로 사용합니다.

| 옵션                        | 기본값                         | 설명                                                                                                                          |
| ------------------------- | --------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `PreferredWidth`          | `360`                       | 콘텐츠가 사용할 너비입니다. effective pixel 단위입니다.                                                                                      |
| `PreferredHeight`         | `48`                        | 콘텐츠가 사용할 높이입니다. effective pixel 단위입니다.                                                                                      |
| `AnimateLayoutChanges`    | `true`                      | 작업 표시줄 안에서 위치나 크기가 바뀔 때 애니메이션을 적용합니다.                                                                                       |
| `LayoutAnimationDuration` | `500`                       | 레이아웃 애니메이션 시간입니다. ms 단위입니다.                                                                                                 |
| `LayoutAnimationEasing`   | `EasingFunctions.CircleOut` | 레이아웃 애니메이션에 사용할 easing delegate(`Func<double, double>`)입니다. 오버슛 없는 기본 easing은 `Deskband11Lib.Core.EasingFunctions`에서 제공합니다. |
| `StartAreaWidth`          | `60`                        | 시작 버튼 영역으로 남겨 둘 너비입니다.                                                                                                      |
| `Placement`               | `BeforeNotificationArea`    | 콘텐츠를 알림 영역 앞에 둘지, 작업 표시줄 버튼 뒤에 둘지 정합니다.                                                                                     |
| `TrackTaskbarButtons`     | `true`                      | UI Automation으로 작업 표시줄 버튼 영역을 추적합니다.                                                                                        |
| `TrackNotificationArea`   | `true`                      | 콘텐츠가 알림 영역을 침범하지 않도록 합니다.                                                                                                   |
| `LayoutRefreshInterval`   | `500 ms`                    | 작업 표시줄 레이아웃을 다시 확인하는 주기입니다.                                                                                                 |

## 내장 Easing 함수

`Deskband11Lib.Core.EasingFunctions`에는 `LayoutAnimationEasing`에 바로 넣을 수 있는 easing 함수들이 준비되어 있습니다.

- `EasingFunctions.Linear`
- `EasingFunctions.SineIn` / `SineOut` / `SineInOut`
- `EasingFunctions.QuadraticIn` / `QuadraticOut` / `QuadraticInOut`
- `EasingFunctions.CubicIn` / `CubicOut` / `CubicInOut`
- `EasingFunctions.QuarticIn` / `QuarticOut` / `QuarticInOut`
- `EasingFunctions.QuinticIn` / `QuinticOut` / `QuinticInOut`
- `EasingFunctions.ExponentialIn` / `ExponentialOut` / `ExponentialInOut`
- `EasingFunctions.CircleIn` / `CircleOut` / `CircleInOut`

필요하면 직접 작성한 `Func<double, double>` delegate를 넘겨도 됩니다.

## 샘플 프로젝트

위 코드는 핵심 API만 보여주는 최소 예제입니다. 실제 작업 표시줄 앱을 만들 때는 창 수명주기, 시작 순서, Explorer 재시작 복구, 프레임워크별 호스팅 처리가 중요하므로 샘플 프로젝트를 먼저 참고하시는 것을 권장합니다.

- `Deskband11Lib.WinUI.Sample`: WinUI 3 및 Windows App SDK 앱용 샘플입니다.
- `Deskband11Lib.Wpf.Sample`: WPF 앱용 샘플입니다. 테두리 없는 투명 호스트 창 설정도 포함되어 있습니다.

## 요구 사항

- Windows 11.
- 선택한 UI 프레임워크와 호환되는 대상 프레임워크.
- WinUI 3는 Windows App SDK가 필요합니다.
- WPF는 프로젝트 파일에 `UseWPF=true`가 필요합니다.

## 프로젝트 개발

```powershell
dotnet restore
dotnet build Deskband11Lib.slnx -c Debug
dotnet publish Deskband11Lib.WinUI.Sample\Deskband11Lib.WinUI.Sample.csproj -c Release -r win-x64
```

## 감사의 말

[zadjii](https://github.com/zadjii)와 [Deskband11](https://github.com/zadjii/Deskband11)에 감사드립니다. Windows 11 작업 표시줄 안에 앱 콘텐츠를 올린다는 핵심 아이디어는 해당 프로젝트에서 시작되었고, Deskband11Lib도 그 훌륭한 발상에서 큰 영감을 받았습니다.

## 라이선스

Deskband11Lib는 [MIT License](LICENSE)로 배포됩니다.

## 작성자

[Howon Lee (airtaxi)](https://github.com/airtaxi)가 만들었습니다.
