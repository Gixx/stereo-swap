using System.IO;
using Microsoft.Win32;

namespace StereoSwap.Tray.Services;

/// <summary>
/// Detects whether the system-wide StereoSwap APO is registered (audiodg path).
/// </summary>
public static class ApoStatus
{
    public const string Clsid = "{B3E8C1A0-7D4F-4E2A-9C1B-5F6A8D0E2B11}";

    public static bool IsComRegistered()
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{Clsid}\InprocServer32");
        var path = key?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        using var apo = Registry.LocalMachine.OpenSubKey(
            $@"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{Clsid}");
        return apo is not null;
    }

    public static string? RegisteredDllPath()
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{Clsid}\InprocServer32");
        return key?.GetValue(null) as string;
    }

    /// <summary>
    /// Hypervisor-enforced Code Integrity (Memory Integrity) blocks unsigned APOs in audiodg.
    /// </summary>
    public static bool IsMemoryIntegrityEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            var enabled = key?.GetValue("Enabled");
            return enabled is int i && i != 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsProtectedAudioDgDisabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Audio");
            var v = key?.GetValue("DisableProtectedAudioDG");
            return v is int i && i == 1;
        }
        catch
        {
            return false;
        }
    }
}
