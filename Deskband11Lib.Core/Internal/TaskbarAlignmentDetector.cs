using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Deskband11Lib.Core.Internal;

internal static class TaskbarAlignmentDetector
{
    private const string AdvancedRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string TaskbarAlignmentValueName = "TaskbarAl";
    private const double LeftAlignmentThresholdRatio = 0.15;

    public static TaskbarAlignment Detect(HWND taskbarWindow, ButtonSpan startButtonSpan)
    {
        var registryAlignment = ReadRegistryAlignment();
        if (registryAlignment != TaskbarAlignment.Unknown) return registryAlignment;

        return InferAlignmentFromPosition(taskbarWindow, startButtonSpan);
    }

    public static TaskbarAlignment ReadRegistryAlignment()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AdvancedRegistryKeyPath);
            if (key?.GetValue(TaskbarAlignmentValueName) is int alignmentValue) return alignmentValue switch
            {
                0 => TaskbarAlignment.Left,
                1 => TaskbarAlignment.Center,
                _ => TaskbarAlignment.Unknown
            };
        }
        catch { }

        return TaskbarAlignment.Unknown;
    }

    private static TaskbarAlignment InferAlignmentFromPosition(HWND taskbarWindow, ButtonSpan startButtonSpan)
    {
        if (!startButtonSpan.IsValid || taskbarWindow.IsNull) return TaskbarAlignment.Unknown;
        if (!PInvoke.GetWindowRect(taskbarWindow, out var taskbarRectangle)) return TaskbarAlignment.Unknown;

        var taskbarWidth = taskbarRectangle.right - taskbarRectangle.left;
        if (taskbarWidth <= 0) return TaskbarAlignment.Unknown;

        return startButtonSpan.Left - taskbarRectangle.left < taskbarWidth * LeftAlignmentThresholdRatio ? TaskbarAlignment.Left : TaskbarAlignment.Center;
    }
}
