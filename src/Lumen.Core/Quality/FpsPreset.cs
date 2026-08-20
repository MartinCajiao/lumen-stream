namespace Lumen.Core.Quality;

/// <summary>First-class refresh options. Native uses the client panel Hz.</summary>
public enum FpsPreset
{
    Native = 0,
    Hz60 = 60,
    Hz90 = 90,
    Hz120 = 120,
    Hz144 = 144,
    Hz165 = 165,
    Hz200 = 200,
    Hz240 = 240
}

public static class FpsPresetExtensions
{
    public static readonly FpsPreset[] All =
    [
        FpsPreset.Native,
        FpsPreset.Hz60,
        FpsPreset.Hz90,
        FpsPreset.Hz120,
        FpsPreset.Hz144,
        FpsPreset.Hz165,
        FpsPreset.Hz200,
        FpsPreset.Hz240
    ];

    /// <summary>
    /// Resolves the effective stream FPS. Fixed presets fall back automatically
    /// to the client panel Hz when the panel cannot show the requested rate.
    /// </summary>
    public static int Resolve(this FpsPreset preset, int panelHz)
    {
        if (preset == FpsPreset.Native)
        {
            return Math.Clamp(panelHz <= 0 ? 60 : panelHz, 30, 240);
        }

        var requested = (int)preset;
        return panelHz > 0 ? Math.Min(requested, panelHz) : requested;
    }

    public static string Label(this FpsPreset preset) =>
        preset == FpsPreset.Native ? "Nativo del cliente" : $"{(int)preset} Hz";
}
