using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace StereoSwap.ApoSetup;

/// <summary>
/// Elevated helper: register StereoSwapApo.dll and bind/unbind FxProperties (L↔R swap).
/// Usage:
///   StereoSwap.ApoSetup.exe enable
///   StereoSwap.ApoSetup.exe disable
///   StereoSwap.ApoSetup.exe install --dll "C:\path\StereoSwapApo.dll"
/// Device id is read from %ProgramData%\StereoSwap\pending-device-id.txt
/// </summary>
internal static class Program
{
    private const string Clsid = "{B3E8C1A0-7D4F-4E2A-9C1B-5F6A8D0E2B11}";

    private static readonly (string Name, RegistryValueKind Kind)[] FxValues =
    [
        ("{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5", RegistryValueKind.String),       // LFX
        ("{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6", RegistryValueKind.String),       // GFX
        ("{d3993a3f-99c2-4402-b5ec-a92a0367664b},5", RegistryValueKind.MultiString), // SFX
        ("{d3993a3f-99c2-4402-b5ec-a92a0367664b},6", RegistryValueKind.MultiString), // MFX
        ("{d3993a3f-99c2-4402-b5ec-a92a0367664b},7", RegistryValueKind.MultiString)  // EFX (Win10/11)
    ];

    private static readonly string ProgramDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StereoSwap");

    private static readonly string LocalLog =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StereoSwap", "last-install.log");

    private static readonly string ProgramDataLog =
        Path.Combine(ProgramDataDir, "last-install.log");

    private static int Main(string[] args)
    {
        Directory.CreateDirectory(ProgramDataDir);
        Directory.CreateDirectory(Path.GetDirectoryName(LocalLog)!);

        try
        {
            if (args.Length == 0)
            {
                Log("ERROR: missing action. Use: enable | disable | install");
                return 1;
            }

            var action = args[0].Trim().ToLowerInvariant();
            Log($"Action={action}");

            switch (action)
            {
                case "install":
                    Install(GetArg(args, "--dll"));
                    break;
                case "enable":
                    EnsureInstalled(GetArg(args, "--dll"));
                    EnableSwap();
                    break;
                case "disable":
                    DisableSwap();
                    break;
                default:
                    Log($"ERROR: unknown action '{action}'");
                    return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static void Install(string? dllPath)
    {
        dllPath ??= FindDll();
        if (dllPath is null || !File.Exists(dllPath))
            throw new FileNotFoundException("StereoSwapApo.dll not found. Pass --dll or build apo\\Build-Apo.bat.");

        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StereoSwap");
        Directory.CreateDirectory(installDir);
        var dest = Path.Combine(installDir, "StereoSwapApo.dll");
        File.Copy(dllPath, dest, overwrite: true);
        RegisterCom(dest);
        Log($"Installed to {dest}");
    }

    private static void EnsureInstalled(string? dllPath)
    {
        var dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StereoSwap", "StereoSwapApo.dll");
        var src = dllPath ?? FindDll();

        // Prefer refreshing from a local build so enable picks up APO fixes.
        if (src is not null && File.Exists(src))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var stoppedAudio = false;
            try
            {
                try
                {
                    File.Copy(src, dest, overwrite: true);
                }
                catch (IOException)
                {
                    // audiodg may lock the DLL — brief Audiosrv stop to release, then always restart.
                    stoppedAudio = true;
                    using var stop = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Stop-Service -Name Audiosrv -Force -ErrorAction SilentlyContinue\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    stop?.WaitForExit(15_000);
                    Thread.Sleep(500);
                    File.Copy(src, dest, overwrite: true);
                }

                RegisterCom(dest);
                Log($"Installed/updated {dest}");
            }
            finally
            {
                if (stoppedAudio)
                {
                    using var start = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Start-Service -Name Audiosrv -ErrorAction SilentlyContinue\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    start?.WaitForExit(15_000);
                    Thread.Sleep(500);
                    Log("Audiosrv restarted after DLL update.");
                }
            }

            return;
        }

        if (!File.Exists(dest))
            throw new FileNotFoundException("StereoSwapApo.dll not found. Pass --dll or build apo\\Build-Apo.bat.");

        RegisterCom(dest);
    }

    private static bool IsComRegistered()
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{Clsid}\InprocServer32");
        var path = key?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        using var apo = Registry.LocalMachine.OpenSubKey(
            $@"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{Clsid}");
        return apo is not null;
    }

    private static void RegisterCom(string dllFullPath)
    {
        using var clsidKey = Registry.ClassesRoot.CreateSubKey($@"CLSID\{Clsid}");
        clsidKey.SetValue(null, "StereoSwap APO");
        using var inproc = clsidKey.CreateSubKey("InprocServer32");
        inproc.SetValue(null, dllFullPath);
        inproc.SetValue("ThreadingModel", "Both");

        // Required for audiodg to accept the APO (same keys OEM / EqAPO write).
        const string apoIface = "{FD7F2B29-24D0-4B5C-B177-592C39F9CA10}"; // IAudioSystemEffects
        using var apoKey = Registry.LocalMachine.CreateSubKey(
            $@"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{Clsid}")
            ?? throw new InvalidOperationException("Cannot create AudioProcessingObjects key.");
        apoKey.SetValue("FriendlyName", "StereoSwap APO");
        apoKey.SetValue("Copyright", "Copyright StereoSwap");
        apoKey.SetValue("MajorVersion", 1, RegistryValueKind.DWord);
        apoKey.SetValue("MinorVersion", 0, RegistryValueKind.DWord);
        apoKey.SetValue("Flags", 0x0d, RegistryValueKind.DWord); // match OEM LFX/GFX
        apoKey.SetValue("MinInputConnections", 1, RegistryValueKind.DWord);
        apoKey.SetValue("MaxInputConnections", 1, RegistryValueKind.DWord);
        apoKey.SetValue("MinOutputConnections", 1, RegistryValueKind.DWord);
        apoKey.SetValue("MaxOutputConnections", 1, RegistryValueKind.DWord);
        apoKey.SetValue("MaxInstances", unchecked((int)0xffffffff), RegistryValueKind.DWord);
        apoKey.SetValue("NumAPOInterfaces", 1, RegistryValueKind.DWord);
        apoKey.SetValue("APOInterface0", apoIface);

        // Required for unsigned APOs (same as Equalizer APO). Without this, audiodg
        // silently skips third-party InprocServer32 DLLs on the protected audio path.
        using (var audioKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Audio"))
        {
            if (audioKey is not null)
            {
                var previous = audioKey.GetValue("DisableProtectedAudioDG");
                audioKey.SetValue("DisableProtectedAudioDG", 1, RegistryValueKind.DWord);
                if (previous is not int i || i != 1)
                    Log("NOTE: DisableProtectedAudioDG was just set — a Windows reboot is required before the APO can load.");
            }
        }

        Log($"Registered CLSID {Clsid} -> {dllFullPath} (+ AudioProcessingObjects, DisableProtectedAudioDG=1)");
    }

    private static string? FindDll()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "StereoSwapApo.dll"),
            Path.Combine(AppContext.BaseDirectory, "InstallHelper", "StereoSwapApo.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StereoSwap", "StereoSwapApo.dll"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "apo", "build", "bin", "StereoSwapApo.dll"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void EnableSwap()
    {
        var endpointGuid = ResolveEndpointGuid();
        var fxPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{endpointGuid}\FxProperties";
        Log($"Binding endpoint {endpointGuid}");

        BackupFxValues(endpointGuid, fxPath);
        UnlockKey(fxPath);
        WriteFxValues(fxPath);
        RestartAudioService();
        Log("Swap ENABLED on device (shared mode).");
    }

    private static void DisableSwap()
    {
        var endpointGuid = ResolveEndpointGuid();
        var fxPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{endpointGuid}\FxProperties";
        Log($"Unbinding endpoint {endpointGuid}");

        UnlockKey(fxPath);
        if (!RestoreFxValues(endpointGuid, fxPath))
            ClearFxValues(fxPath);

        RestartAudioService();
        Log("Swap DISABLED on device.");
    }

    private static string ResolveEndpointGuid()
    {
        var pending = Path.Combine(ProgramDataDir, "pending-device-id.txt");
        if (!File.Exists(pending))
            throw new InvalidOperationException("pending-device-id.txt missing.");

        var id = File.ReadAllText(pending).Trim().Trim('\'').Trim('"');
        // {0.0.0.00000000}.{guid} → {guid}
        var dot = id.LastIndexOf('.');
        if (dot >= 0 && dot < id.Length - 1)
            id = id[(dot + 1)..];

        if (!id.StartsWith('{') || !id.EndsWith('}'))
            throw new InvalidOperationException($"Cannot parse endpoint GUID from '{id}'.");

        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{id}");
        if (key is null)
            throw new InvalidOperationException($"Render endpoint not found: {id}");

        return id;
    }

    private static void BackupFxValues(string endpointGuid, string fxPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(fxPath);
        if (key is null)
            return;

        var backup = new Dictionary<string, JsonElement>();
        var onlyOurs = true;
        foreach (var (name, kind) in FxValues)
        {
            var value = key.GetValue(name);
            if (value is null)
                continue;

            // Never overwrite a real OEM backup with our own CLSID (re-enable pollution).
            var asOurs = kind == RegistryValueKind.MultiString
                ? value is string[] arr && arr.Length == 1 && string.Equals(arr[0], Clsid, StringComparison.OrdinalIgnoreCase)
                : string.Equals(Convert.ToString(value), Clsid, StringComparison.OrdinalIgnoreCase);
            if (!asOurs)
                onlyOurs = false;

            backup[name] = kind switch
            {
                RegistryValueKind.MultiString => JsonSerializer.SerializeToElement((string[])value),
                _ => JsonSerializer.SerializeToElement(Convert.ToString(value) ?? string.Empty)
            };
        }

        if (backup.Count == 0 || onlyOurs)
        {
            Log("Skipping Fx backup (empty or already StereoSwap-only — preserve previous OEM backup).");
            return;
        }

        var path = Path.Combine(ProgramDataDir, $"fx-backup-{endpointGuid}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
        Log($"Backed up FxProperties to {path}");
    }

    private static bool RestoreFxValues(string endpointGuid, string fxPath)
    {
        var path = Path.Combine(ProgramDataDir, $"fx-backup-{endpointGuid}.json");
        if (!File.Exists(path))
            return false;

        var backup = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path));
        if (backup is null || backup.Count == 0)
            return false;

        using var key = Registry.LocalMachine.CreateSubKey(fxPath, writable: true)
                       ?? throw new InvalidOperationException($"Cannot open {fxPath}");

        foreach (var (name, kind) in FxValues)
        {
            if (!backup.TryGetValue(name, out var el))
            {
                try { key.DeleteValue(name, throwOnMissingValue: false); } catch { /* ignore */ }
                continue;
            }

            if (kind == RegistryValueKind.MultiString)
            {
                var arr = el.ValueKind == JsonValueKind.Array
                    ? el.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                    : [el.GetString() ?? string.Empty];
                key.SetValue(name, arr, RegistryValueKind.MultiString);
            }
            else
            {
                key.SetValue(name, el.GetString() ?? string.Empty, RegistryValueKind.String);
            }
        }

        Log($"Restored FxProperties from {path}");
        return true;
    }

    private static void WriteFxValues(string fxPath)
    {
        using var key = Registry.LocalMachine.CreateSubKey(fxPath, writable: true)
                       ?? throw new InvalidOperationException($"Cannot open {fxPath} for write");

        foreach (var (name, kind) in FxValues)
        {
            if (kind == RegistryValueKind.MultiString)
                key.SetValue(name, new[] { Clsid }, RegistryValueKind.MultiString);
            else
                key.SetValue(name, Clsid, RegistryValueKind.String);
        }

        // PKEY_AudioEndpoint_Disable_SysFx = 0 → enhancements on (otherwise APO never runs).
        key.SetValue("{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5", 0, RegistryValueKind.DWord);

        Log($"Wrote StereoSwap CLSID into {fxPath}");
    }

    private static void ClearFxValues(string fxPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(fxPath, writable: true);
        if (key is null)
            return;

        foreach (var (name, _) in FxValues)
        {
            try { key.DeleteValue(name, throwOnMissingValue: false); } catch { /* ignore */ }
        }

        Log($"Cleared StereoSwap values from {fxPath}");
    }

    private static void UnlockKey(string relativePath)
    {
        TokenPrivileges.Enable("SeTakeOwnershipPrivilege");
        TokenPrivileges.Enable("SeRestorePrivilege");

        var user = WindowsIdentity.GetCurrent().User
                   ?? throw new InvalidOperationException("Cannot resolve current user SID.");

        using (var key = Registry.LocalMachine.OpenSubKey(
                   relativePath,
                   RegistryKeyPermissionCheck.ReadWriteSubTree,
                   RegistryRights.TakeOwnership))
        {
            if (key is null)
            {
                // Parent may exist; try create after unlocking parent.
                var parent = relativePath[..relativePath.LastIndexOf('\\')];
                UnlockKey(parent);
                Registry.LocalMachine.CreateSubKey(relativePath)?.Dispose();
                return;
            }

            var acl = key.GetAccessControl(AccessControlSections.None);
            acl.SetOwner(user);
            key.SetAccessControl(acl);
        }

        using (var key = Registry.LocalMachine.OpenSubKey(
                   relativePath,
                   RegistryKeyPermissionCheck.ReadWriteSubTree,
                   RegistryRights.ChangePermissions | RegistryRights.ReadKey))
        {
            if (key is null)
                throw new InvalidOperationException($"Cannot reopen key for permissions: {relativePath}");

            var acl = key.GetAccessControl();
            var rule = new RegistryAccessRule(
                user,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            acl.SetAccessRule(rule);
            key.SetAccessControl(acl);
        }

        Log($"Unlocked ACL on {relativePath}");
    }

    private static void RestartAudioService()
    {
        try
        {
            // Only bounce Audiosrv. Do NOT stop AudioEndpointBuilder — if Audiosrv
            // fails to come back, Windows shows zero playback devices.
            Log("Restarting Windows Audio service (Audiosrv)...");
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"try { Restart-Service -Name Audiosrv -Force -ErrorAction Stop } catch { Start-Service -Name Audiosrv -ErrorAction SilentlyContinue }; Start-Sleep -Seconds 1; if ((Get-Service Audiosrv).Status -ne 'Running') { Start-Service Audiosrv -ErrorAction SilentlyContinue }\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit(45_000);
            Thread.Sleep(1000);

            using var check = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"(Get-Service Audiosrv).Status\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            var status = check?.StandardOutput.ReadToEnd().Trim() ?? "?";
            check?.WaitForExit(10_000);
            Log($"Audiosrv status after restart: {status}");
            if (!status.Equals("Running", StringComparison.OrdinalIgnoreCase))
                Log("WARN: Audiosrv is not Running — start it manually: Start-Service Audiosrv");
        }
        catch (Exception ex)
        {
            Log("WARN: audio service restart failed: " + ex.Message);
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"Start-Service Audiosrv -ErrorAction SilentlyContinue\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(15_000);
            }
            catch { /* ignore */ }
        }
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        foreach (var path in new[] { LocalLog, ProgramDataLog })
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch
            {
                // ignore
            }
        }

        Console.WriteLine(message);
    }
}

internal static class TokenPrivileges
{
    private const int SePrivilegeEnabled = 0x00000002;
    private const int TokenAdjustPrivileges = 0x0020;
    private const int TokenQuery = 0x0008;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TokPriv1Luid
    {
        public int Count;
        public long Luid;
        public int Attr;
    }

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall,
        ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr rel);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? host, string name, ref long pluid);

    public static void Enable(string name)
    {
        var htok = IntPtr.Zero;
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, ref htok))
            return;

        var tp = new TokPriv1Luid { Count = 1, Luid = 0, Attr = SePrivilegeEnabled };
        if (!LookupPrivilegeValue(null, name, ref tp.Luid))
            return;

        AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }
}
