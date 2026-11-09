using Microsoft.Win32;

namespace Deskband11Lib.Core.Internal;

internal static class TaskbarAlignmentDetector
{
    private const string AdvancedRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string TaskbarAlignmentValueName = "TaskbarAl";
    private const double LeftAlignmentThresholdRatio = 0.15;

    public static TaskbarAlignment Detect(TaskbarAxisRectangle taskbarRectangle, AxisSpan startButtonSpan)
    {
        var registryAlignment = ReadRegistryAlignment();
        if (registryAlignment != TaskbarAlignment.Unknown) return registryAlignment;

        return InferAlignmentFromPosition(taskbarRectangle, startButtonSpan);
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

    // On a vertical taskbar, Left stands for top-aligned icons because the position is measured along the taskbar.
    private static TaskbarAlignment InferAlignmentFromPosition(TaskbarAxisRectangle taskbarRectangle, AxisSpan startButtonSpan)
    {
        if (!startButtonSpan.IsValid || taskbarRectangle.MainLength <= 0) return TaskbarAlignment.Unknown;
        return startButtonSpan.Start - taskbarRectangle.MainStart < taskbarRectangle.MainLength * LeftAlignmentThresholdRatio ? TaskbarAlignment.Left : TaskbarAlignment.Center;
    }
}
