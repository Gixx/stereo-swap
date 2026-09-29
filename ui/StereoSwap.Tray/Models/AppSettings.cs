namespace StereoSwap.Tray.Models;

public sealed class AudioRenderDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    /// <summary>Heuristic exclude list (e.g. Beacn Mix Create).</summary>
    public bool IsExcluded { get; init; }

    public override string ToString() => IsDefault ? $"{Name} (default)" : Name;
}

public sealed class AppSettings
{
    public string? SelectedDeviceId { get; set; }
    public bool SwapEnabled { get; set; }
    public bool Autostart { get; set; }
    public List<string> EnabledDeviceIds { get; set; } = [];
}
