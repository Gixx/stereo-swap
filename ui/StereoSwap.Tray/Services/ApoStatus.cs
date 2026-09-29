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
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public static string? RegisteredDllPath()
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{Clsid}\InprocServer32");
        return key?.GetValue(null) as string;
    }
}
