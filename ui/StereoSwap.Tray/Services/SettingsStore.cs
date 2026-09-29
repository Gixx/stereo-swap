using System.IO;
using System.Text.Json;
using StereoSwap.Tray.Models;

namespace StereoSwap.Tray.Services;

/// <summary>
/// Persists tray settings under LocalAppData and mirrors enabled device IDs
/// to ProgramData so the APO (audiodg) can read them.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _userSettingsPath;
    private readonly string _apoConfigPath;

    public SettingsStore()
    {
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StereoSwap");
        Directory.CreateDirectory(local);
        _userSettingsPath = Path.Combine(local, "settings.json");

        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "StereoSwap");
        Directory.CreateDirectory(programData);
        _apoConfigPath = Path.Combine(programData, "config.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_userSettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_userSettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var userJson = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_userSettingsPath, userJson);

        // APO-facing subset (machine-wide, readable by audiodg).
        var apoPayload = new
        {
            enabledDeviceIds = settings.SwapEnabled && !string.IsNullOrWhiteSpace(settings.SelectedDeviceId)
                ? new[] { settings.SelectedDeviceId }
                : Array.Empty<string>()
        };
        settings.EnabledDeviceIds = apoPayload.enabledDeviceIds.ToList();
        File.WriteAllText(_apoConfigPath, JsonSerializer.Serialize(apoPayload, JsonOptions));
    }
}
