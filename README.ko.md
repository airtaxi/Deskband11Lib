# Deskband11Lib

[![NuGet](https://img.shields.io/nuget/v/Deskband11Lib.svg)](https://www.nuget.org/packages/Deskband11Lib)
[![NuGet downloads](https://img.shields.io/nuget/dt/Deskband11Lib.svg)](https://www.nuget.org/packages/Deskband11Lib)
[![Pack and Publish](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml/badge.svg)](https://github.com/airtaxi/Deskband11Lib/actions/workflows/pack-and-publish.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

🌐 [English](README.md) | 한국어

Deskband11Lib는 Windows 11 작업 표시줄 안에 WinUI 3 기반 콘텐츠를 자연스럽게 배치할 수 있게 해주는 라이브러리입니다. 작은 대시보드, 빠른 제어 패널, 상태 표시기, 미디어 위젯, 런처, 생산성 도구처럼 항상 보여야 하는 기능을 데스크톱의 일부처럼 만들 수 있습니다.

![Deskband11Lib 스크린샷](.github/screenshot.png)

## 주요 기능

- 앱에서 쓰는 WinUI 3 컨트롤, 스타일, composition 기능 그대로 작업 표시줄 위젯을 만들 수 있습니다.
- 전체 창을 열지 않아도 사용자가 바로 확인할 수 있는 위치에 실시간 정보를 보여줄 수 있습니다.
- 타이머, 미디어 재생, 계정 전환, 빌드 상태, 장치 모니터링, 빠른 실행 같은 기능을 작고 밀도 있게 배치할 수 있습니다.
- 고정된 앱, 실행 중인 앱, 알림 영역과 겹치지 않도록 사용 가능한 작업 표시줄 공간에 맞춰 콘텐츠를 배치합니다.
- Explorer가 재시작되어도 애플리케이션이 호스팅 창을 안전하게 다시 만들 수 있도록 알려줍니다.
- Windows App SDK 앱과 NativeAOT 게시를 지원합니다.

## 설치

```powershell
dotnet add package Deskband11Lib
```

## 기본 사용법

WinUI 창을 만들고 `TaskbarContentHost`를 생성한 다음, 초기 작업 표시줄 레이아웃 준비가 끝난 뒤 attach하고 창을 활성화합니다.

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

Explorer가 재시작되면 작업 표시줄이 기존 자식 창을 파괴합니다. 이때 가장 안전한 복구 방식은 기존 WinUI `Window`를 재사용하지 않고, 애플리케이션이 새 창을 만들어 다시 attach하는 것입니다.

```csharp
host.TaskbarWindowRecreated += async (_, _) =>
{
    await RecreateMainWindowAsync();
};
```

## 동작 방식

Deskband11Lib는 WinUI 앱에 작업 표시줄 크기의 표시 영역을 제공하고, 실제 작업 표시줄 레이아웃에 맞춰 그 영역을 계속 정렬합니다. 내부적으로는 일반적인 Win32 창 부모 관계를 이용합니다.

- 기본 작업 표시줄 창인 `Shell_TrayWnd`를 찾습니다.
- 애플리케이션이 만든 일반 WinUI `Window`를 받습니다.
- WinUI 창 스타일을 최상위 popup 창에서 자식 창 스타일로 바꿉니다.
- `SetParent`로 WinUI 창을 작업 표시줄 아래에 붙입니다.
- 작업 표시줄 버튼과 알림 영역 사이에서 사용할 수 있는 사각형을 계산합니다.
- `SetWindowPos`와 `SetWindowRgn`으로 호스팅된 창의 위치와 클리핑 영역을 조정합니다.

현재 Windows 11에서는 작업 표시줄의 자식 HWND 계층만으로 실제 작업 표시줄 버튼 폭을 안정적으로 얻기 어렵습니다. 그래서 Deskband11Lib는 UI Automation으로 작업 표시줄에 보이는 버튼들의 위치를 확인합니다. UI Automation 스캔은 UI thread 밖에서 실행되고 캐시되므로, 레이아웃 갱신이 호스팅된 WinUI 콘텐츠를 막지 않습니다.

## Explorer 재시작 처리

작업 표시줄은 Explorer가 소유합니다. Explorer가 크래시되거나 재시작되면 기존 작업 표시줄 창과 그 아래에 붙어 있던 자식 창도 함께 파괴됩니다. 이 상태에서 이전 WinUI `Window`를 다시 붙이는 방식은 불안정할 수 있으므로, Deskband11Lib는 자동 reattach 대신 작업 표시줄 재생성 이벤트를 애플리케이션에 알려줍니다.

애플리케이션은 `TaskbarWindowRecreated` 이벤트를 처리해 기존 host/window를 정리하고, 새 창을 만든 뒤 `AttachWhenLayoutReadyAsync()`를 다시 호출하는 흐름을 사용하면 됩니다.

## 옵션

- `PreferredWidth`: 콘텐츠가 원하는 너비입니다. effective pixel 단위입니다.
- `PreferredHeight`: 콘텐츠가 원하는 높이입니다. effective pixel 단위입니다.
- `StartAreaWidth`: 시작 버튼 영역으로 예약할 너비입니다.
- `Placement`: 알림 영역 앞 또는 작업 표시줄 버튼 뒤쪽 중 배치 위치를 선택합니다.
- `TrackTaskbarButtons`: UI Automation 기반 작업 표시줄 버튼 측정을 사용할지 정합니다.
- `TrackNotificationArea`: 콘텐츠가 알림 영역을 침범하지 않도록 합니다.
- `LayoutRefreshInterval`: 작업 표시줄 레이아웃을 다시 확인하는 주기입니다.

## 요구 사항

- Windows 11.
- WinUI 3 / Windows App SDK.
- Windows App SDK와 호환되는 Windows target framework.

샘플 프로젝트는 `net10.0-windows10.0.26100.0`을 대상으로 하며 NativeAOT 게시를 지원합니다.

## 프로젝트 개발

```powershell
dotnet restore
dotnet build Deskband11Lib.slnx -c Debug
dotnet publish Deskband11Lib.Sample\Deskband11Lib.Sample.csproj -c Release -r win-x64
```

## 감사의 말

[zadjii](https://github.com/zadjii)와 [Deskband11](https://github.com/zadjii/Deskband11)에 감사드립니다. Windows 11 작업 표시줄 안에 애플리케이션 콘텐츠를 가져온다는 핵심 아이디어는 해당 프로젝트에서 비롯되었으며, Deskband11Lib는 그 놀라운 발상에서 큰 영감을 받았습니다.

## 라이선스

Deskband11Lib는 [MIT License](LICENSE)로 배포됩니다.

## 작성자

[Howon Lee (airtaxi)](https://github.com/airtaxi)가 만들었습니다.
