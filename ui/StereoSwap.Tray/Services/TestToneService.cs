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

    /// <param name="logicalLeft">True = user pressed Left (expect physical left when swap corrects wiring).</param>
    /// <param name="swapEnabled">When true, write the tone to the opposite WAV channel (in-app preview of L↔R).</param>
    private void PlayIsolatedChannel(bool logicalLeft, bool swapEnabled)
    {
        // Hardware already swapped + software swap ON ⇒ tone for "Left" must go on WAV right, etc.
        var toneOnLeftChannel = logicalLeft ^ swapEnabled;
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
                    // ignore playback failures (no device, etc.)
                }
            }, token);
        }
    }

    /// <summary>
    /// Builds a PCM WAV: sine on one channel, silence on the other.
    /// </summary>
    internal static byte[] BuildStereoWav(bool leftChannel)
    {
        var frameCount = (int)(SampleRate * DurationSeconds);
        var dataBytes = frameCount * 4; // 2 ch * 16-bit
        using var ms = new MemoryStream(44 + dataBytes);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataBytes);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);                 // PCM chunk size
        bw.Write((short)1);           // PCM
        bw.Write((short)2);           // stereo
        bw.Write(SampleRate);
        bw.Write(SampleRate * 4);     // byte rate
        bw.Write((short)4);           // block align
        bw.Write((short)16);          // bits
        bw.Write("data"u8);
        bw.Write(dataBytes);

        var fadeSamples = Math.Min(SampleRate / 50, frameCount / 10); // ~20 ms
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
/// Thin wrapper that launches the elevated install helper for FxProperties bind/unbind.
/// </summary>
public sealed class ApoInstallClient
{
    public string? ResolveHelperPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "InstallHelper", "RegisterApo.ps1"),
            Path.Combine(baseDir, "RegisterApo.ps1"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "install", "RegisterApo.ps1")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "install", "RegisterApo.ps1"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public bool TryBindDevice(string deviceId, bool enable, out string message)
    {
        var script = ResolveHelperPath();
        if (script is null)
        {
            message = "RegisterApo.ps1 not found. Bind manually via install\\RegisterApo.ps1";
            return false;
        }

        var action = enable ? "bind" : "unbind";
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Action {action} -DeviceId \"{deviceId}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var proc = Process.Start(psi);
            proc?.WaitForExit(60_000);
            message = enable ? "APO bound (admin)." : "APO unbound (admin).";
            return proc is { ExitCode: 0 };
        }
        catch (Exception ex)
        {
            message = $"Install helper error: {ex.Message}";
            return false;
        }
    }
}
