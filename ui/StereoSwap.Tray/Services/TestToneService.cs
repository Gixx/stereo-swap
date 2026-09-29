using System.Diagnostics;
using System.IO;
using System.Media;

namespace StereoSwap.Tray.Services;

/// <summary>
/// Plays short L-only / R-only tones (AudioCheck-style stereo channel check).
/// See https://www.audiocheck.net/audiotests_stereo.php
/// </summary>
public sealed class TestToneService
{
    private const int SampleRate = 44_100;
    private const double FrequencyHz = 880.0;
    private const double DurationSeconds = 0.85;
    private const double Amplitude = 0.28;

    private readonly object _playLock = new();
    private CancellationTokenSource? _cts;

    public void PlayLeftOnly(bool swapEnabled = false) =>
        PlayIsolatedChannel(logicalLeft: true, swapEnabled);

    public void PlayRightOnly(bool swapEnabled = false) =>
        PlayIsolatedChannel(logicalLeft: false, swapEnabled);

    private void PlayIsolatedChannel(bool logicalLeft, bool swapEnabled)
    {
        var previewInApp = swapEnabled && !ApoStatus.IsComRegistered();
        var toneOnLeftChannel = logicalLeft ^ previewInApp;
        PlayOnWavChannel(leftChannel: toneOnLeftChannel);
    }

    private void PlayOnWavChannel(bool leftChannel)
    {
        lock (_playLock)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var wav = BuildStereoWav(leftChannel);

            _ = Task.Run(() =>
            {
                try
                {
                    using var ms = new MemoryStream(wav);
                    using var player = new SoundPlayer(ms);
                    if (token.IsCancellationRequested)
                        return;
                    player.PlaySync();
                }
                catch
                {
                    // ignore playback failures
                }
            }, token);
        }
    }

    internal static byte[] BuildStereoWav(bool leftChannel)
    {
        var frameCount = (int)(SampleRate * DurationSeconds);
        var dataBytes = frameCount * 4;
        using var ms = new MemoryStream(44 + dataBytes);
        using var bw = new BinaryWriter(ms);

        bw.Write("RIFF"u8);
        bw.Write(36 + dataBytes);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)2);
        bw.Write(SampleRate);
        bw.Write(SampleRate * 4);
        bw.Write((short)4);
        bw.Write((short)16);
        bw.Write("data"u8);
        bw.Write(dataBytes);

        var fadeSamples = Math.Min(SampleRate / 50, frameCount / 10);
        for (var i = 0; i < frameCount; i++)
        {
            var envelope = 1.0;
            if (i < fadeSamples)
                envelope = i / (double)fadeSamples;
            else if (i > frameCount - fadeSamples)
                envelope = (frameCount - i) / (double)fadeSamples;

            var sample = (short)(Math.Sin(2 * Math.PI * FrequencyHz * i / SampleRate) * Amplitude * envelope * short.MaxValue);
            short left = leftChannel ? sample : (short)0;
            short right = leftChannel ? (short)0 : sample;
            bw.Write(left);
            bw.Write(right);
        }

        bw.Flush();
        return ms.ToArray();
    }
}

/// <summary>
/// Launches elevated StereoSwap.ApoSetup.exe for install / enable / disable.
/// </summary>
public sealed class ApoInstallClient
{
    public string? ResolveSetupExe()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "StereoSwap.ApoSetup.exe"),
            Path.Combine(baseDir, "InstallHelper", "StereoSwap.ApoSetup.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "ui", "StereoSwap.ApoSetup", "bin", "Debug", "net10.0-windows", "StereoSwap.ApoSetup.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "ui", "StereoSwap.ApoSetup", "bin", "Release", "net10.0-windows", "StereoSwap.ApoSetup.exe"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public string? ResolveDllPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StereoSwap", "StereoSwapApo.dll"),
            Path.Combine(AppContext.BaseDirectory, "StereoSwapApo.dll"),
            Path.Combine(AppContext.BaseDirectory, "InstallHelper", "StereoSwapApo.dll"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "apo", "build", "bin", "StereoSwapApo.dll"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public bool TrySetSwap(string deviceId, bool enable, out string message)
    {
        var setup = ResolveSetupExe();
        if (setup is null)
        {
            message = "StereoSwap.ApoSetup.exe not found. Rebuild the solution (F5).";
            return false;
        }

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "StereoSwap");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(
            Path.Combine(dataDir, "pending-device-id.txt"),
            deviceId.Trim().Trim('\'').Trim('"'));

        var action = enable ? "enable" : "disable";
        var args = action;
        var dll = ResolveDllPath();
        if (enable && dll is not null)
            args += $" --dll \"{dll}\"";

        var psi = new ProcessStartInfo
        {
            FileName = setup,
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                message = "Could not start ApoSetup (UAC cancelled?).";
                return false;
            }

            proc.WaitForExit(120_000);
            var logHint = ReadLastInstallLog();
            if (proc.ExitCode != 0)
            {
                message = (enable ? "APO enable failed." : "APO disable failed.")
                          + (logHint is null
                              ? " See %LocalAppData%\\StereoSwap\\last-install.log"
                              : $" {logHint}");
                return false;
            }

            message = enable
                ? "System swap ON on selected hardware. Reboot once if this is the first install. Shared mode only (Beacn→Optical OK)."
                : "System swap OFF (APO unbound).";
            return true;
        }
        catch (Exception ex)
        {
            message = $"ApoSetup error: {ex.Message}";
            return false;
        }
    }

    private static string? ReadLastInstallLog()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StereoSwap", "last-install.log"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StereoSwap", "last-install.log")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                    continue;
                var text = File.ReadAllText(path, System.Text.Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(text))
                    continue;
                var lines = text.Replace("\r\n", "\n").Split('\n');
                var error = lines.LastOrDefault(l => l.Contains("ERROR:", StringComparison.OrdinalIgnoreCase));
                return error ?? lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
