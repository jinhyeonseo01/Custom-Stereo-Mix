using Microsoft.Win32;
using CustomStereoMix.Core;

namespace CustomStereoMix.App;

public sealed class WindowsStartupService(string executablePath, string valueName = WindowsStartupService.ValueName) : IStartupService
{
    public const string ValueName = "CustomStereoMix";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private readonly string _command = $"\"{Path.GetFullPath(executablePath)}\" --autostart";
    public bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey);
            var disabled = approval?.GetValue(valueName) is byte[] data && data.Length > 0 && data[0] is 3 or 7;
            return !disabled && string.Equals(run?.GetValue(valueName) as string, _command, StringComparison.OrdinalIgnoreCase);
        }
    }
    public void SetEnabled(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) run.SetValue(valueName, _command, RegistryValueKind.String);
        else run.DeleteValue(valueName, throwOnMissingValue: false);
        // Reset only this app's Windows approval metadata when the user changes
        // the choice. Never delete the shared Run/StartupApproved keys.
        using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey, writable: true);
        approval?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
