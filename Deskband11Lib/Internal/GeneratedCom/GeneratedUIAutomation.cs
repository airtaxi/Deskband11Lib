using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Deskband11Lib.Internal.GeneratedCom;

internal enum GeneratedTreeScope
{
    Children = 2,
    Descendants = 4
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct GeneratedRectangle
{
    public readonly int Left;
    public readonly int Top;
    public readonly int Right;
    public readonly int Bottom;
}

[GeneratedComInterface]
[Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IGeneratedUIAutomation
{
    void CompareElements();

    void CompareRuntimeIds();

    void GetRootElement();

    IGeneratedUIAutomationElement ElementFromHandle(nint windowHandle);

    void ElementFromPoint();

    void GetFocusedElement();

    void GetRootElementBuildCache();

    void ElementFromHandleBuildCache();

    void ElementFromPointBuildCache();

    void GetFocusedElementBuildCache();

    void CreateTreeWalker();

    void GetControlViewWalker();

    void GetContentViewWalker();

    void GetRawViewWalker();

    void GetRawViewCondition();

    void GetControlViewCondition();

    void GetContentViewCondition();

    void CreateCacheRequest();

    IGeneratedUIAutomationCondition CreateTrueCondition();
}

[GeneratedComInterface]
[Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IGeneratedUIAutomationElement
{
    void SetFocus();

    void GetRuntimeId();

    void FindFirst();

    IGeneratedUIAutomationElementArray FindAll(GeneratedTreeScope scope, IGeneratedUIAutomationCondition condition);

    void FindFirstBuildCache();

    void FindAllBuildCache();

    void BuildUpdatedCache();

    void GetCurrentPropertyValue();

    void GetCurrentPropertyValueEx();

    void GetCachedPropertyValue();

    void GetCachedPropertyValueEx();

    void GetCurrentPatternAs();

    void GetCachedPatternAs();

    void GetCurrentPattern();

    void GetCachedPattern();

    void GetCachedParent();

    void GetCachedChildren();

    void GetCurrentProcessIdentifier();

    [PreserveSig]
    int GetCurrentControlType(out int controlType);

    void GetCurrentLocalizedControlType();

    void GetCurrentName();

    void GetCurrentAcceleratorKey();

    void GetCurrentAccessKey();

    void GetCurrentHasKeyboardFocus();

    void GetCurrentIsKeyboardFocusable();

    void GetCurrentIsEnabled();

    void GetCurrentAutomationIdentifier();

    void GetCurrentClassName();

    void GetCurrentHelpText();

    void GetCurrentCulture();

    void GetCurrentIsControlElement();

    void GetCurrentIsContentElement();

    void GetCurrentIsPassword();

    void GetCurrentNativeWindowHandle();

    void GetCurrentItemType();

    [PreserveSig]
    int GetCurrentIsOffscreen(out int isOffscreen);

    void GetCurrentOrientation();

    void GetCurrentFrameworkIdentifier();

    void GetCurrentIsRequiredForForm();

    void GetCurrentItemStatus();

    [PreserveSig]
    int GetCurrentBoundingRectangle(out GeneratedRectangle boundingRectangle);
}

[GeneratedComInterface]
[Guid("14314595-B4BC-4055-95F2-58F2E42C9855")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IGeneratedUIAutomationElementArray
{
    int GetLength();

    IGeneratedUIAutomationElement GetElement(int index);
}

[GeneratedComInterface]
[Guid("352FFBA8-0973-437C-A61F-F64CAFD81DF9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IGeneratedUIAutomationCondition;
