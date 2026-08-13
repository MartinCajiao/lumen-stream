namespace Lumen.Core.Quality;

public enum QualityMode
{
    /// <summary>4:2:0, adaptive bitrate, lowest latency. For games.</summary>
    Game,

    /// <summary>4:4:4, HEVC/AV1, 80–150 Mbps LAN. For text and "impeccable" image.</summary>
    SharpLan
}

public static class QualityModeExtensions
{
    public static bool UsesYuv444(this QualityMode mode) => mode == QualityMode.SharpLan;

    public static string Label(this QualityMode mode) => mode switch
    {
        QualityMode.Game => "Juego (4:2:0, más FPS)",
        QualityMode.SharpLan => "Nítido LAN (4:4:4, 80–150 Mbps)",
        _ => mode.ToString()
    };
}
